#!/usr/bin/env python3
"""云端账号集成验证：只生成 qa_ 前缀测试玩家，不打印密码、邀请码或令牌。"""
import concurrent.futures
import hashlib
import json
import pathlib
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE = "http://127.0.0.1:5080"
HOST = "/opt/project-star/api-current/host/ProjectStar.Server"
DATABASE = "project_star"
ENV = ["PROJECT_STAR_DATA_DIR=/var/lib/project-star",
       "PROJECT_STAR_DATABASE=Host=/var/run/postgresql;Database=project_star;Username=project-star"]
PASSED = []
SENSITIVE = []


def run(args, input_value=None):
    result = subprocess.run(args, input=input_value, text=True, capture_output=True, timeout=60, cwd="/")
    if result.returncode:
        # 子进程输入输出可能涉及凭证，不纳入异常与报告。
        raise RuntimeError("subprocess_failed: " + pathlib.Path(args[0]).name)
    return result.stdout.strip()


def admin(*args, password=None):
    return run(["sudo", "-n", "-u", "project-star", "env", *ENV, HOST, *args], password)


def sql(statement, database=DATABASE):
    return run(["sudo", "-n", "-u", "project-star", "psql", "-v", "ON_ERROR_STOP=1",
                "-d", database, "-Atqc", statement])


def request(path, data=None, token=None, raw=None, extra=None):
    headers = {"Content-Type": "application/json", **(extra or {})}
    if token:
        headers["Authorization"] = "Bearer " + token
    payload = raw if raw is not None else (json.dumps(data).encode() if data is not None else None)
    req = urllib.request.Request(BASE + path, data=payload, headers=headers)
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read()
        parsed = json.loads(body) if body else None
        return response.status, parsed, response.headers


def check(name, condition):
    if not condition:
        raise AssertionError(name)
    PASSED.append(name)
    print("PASS " + name, flush=True)


def restart():
    run(["sudo", "-n", "systemctl", "restart", "project-star-api"])
    for _ in range(40):
        try:
            if request("/health/ready")[0] == 200:
                return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(0.25)
    raise AssertionError("restart_readiness")


def invite(uses=1):
    value = admin("create-invite", str(uses), "1")
    SENSITIVE.append(value)
    return value


def password():
    value = secrets.token_urlsafe(24)
    SENSITIVE.append(value)
    return value


def remember(session):
    SENSITIVE.extend([session["accessToken"], session["refreshToken"]])
    return session


def main():
    restart()
    match_query = "SELECT md5(COALESCE(string_agg(match_id::text || ':' || version || ':' || state::text, '|' ORDER BY match_id), '')) FROM matches"
    has_matches = sql("SELECT to_regclass('public.matches') IS NOT NULL") == "t"
    original_matches = sql(match_query) if has_matches else None
    pvp_queries = [
        "SELECT md5(COALESCE(string_agg(battle_id::text || ':' || input_state || ':' || status || ':' || COALESCE(result::text,''), '|' ORDER BY battle_id),'')) FROM pvp_battles",
        "SELECT md5(COALESCE(string_agg(snapshot_id::text || ':' || state, '|' ORDER BY snapshot_id),'')) FROM pvp_snapshots",
    ] if sql("SELECT to_regclass('public.pvp_battles') IS NOT NULL") == "t" else []
    original_pvp = [sql(query) for query in pvp_queries]
    prefix = "qa_" + uuid.uuid4().hex[:12]
    pwd = password()
    shared = invite(2)
    check("ready", request("/health/ready")[0] == 200)
    check("unauthenticated_rejected", request("/v1/account")[0] == 401)
    code, first, headers = request("/v1/auth/register", {"username": prefix + "A", "password": pwd, "invitation": shared})
    check("invited_registration", code == 201 and headers.get("Cache-Control") == "no-store")
    remember(first)
    code, second, _ = request("/v1/auth/register", {"username": prefix + "B", "password": pwd, "invitation": shared})
    check("second_distinct_player", code == 201 and second["playerId"] != first["playerId"])
    remember(second)
    code, _, _ = request("/v1/auth/register", {"username": prefix + "C", "password": pwd, "invitation": shared})
    check("invitation_use_cap", code == 400)
    code, view, _ = request("/v1/account?playerId=" + second["playerId"], token=first["accessToken"],
                            extra={"X-Player-ID": second["playerId"]})
    check("identity_from_session_only", code == 200 and view["playerId"] == first["playerId"])
    check("invalid_password_rejected", request("/v1/auth/login", {"username": prefix + "A", "password": password()})[0] == 401)
    check("unknown_account_same_error", request("/v1/auth/login", {"username": prefix + "unknown", "password": pwd})[1] == {"error": "authentication_failed"})
    code, login, _ = request("/v1/auth/login", {"username": (prefix + "A").upper(), "password": pwd})
    check("normalized_login_same_player", code == 200 and login["playerId"] == first["playerId"])
    remember(login)
    replacement = invite()
    check("duplicate_registration_rejected", request("/v1/auth/register", {"username": (prefix + "A").upper(), "password": pwd, "invitation": replacement})[0] == 400)
    code, third, _ = request("/v1/auth/register", {"username": prefix + "C", "password": pwd, "invitation": replacement})
    check("failed_registration_preserves_invite", code == 201)
    remember(third)

    # 新令牌只允许一个并发请求拿到；避免通过重试测试掩盖竞态。
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(request, "/v1/auth/refresh", {"refreshToken": login["refreshToken"]}) for _ in range(2)]
        results = [future.result() for future in futures]
    check("concurrent_refresh_one_winner", sorted(item[0] for item in results) == [200, 401])
    rotated = remember(next(item[1] for item in results if item[0] == 200))
    check("rotated_access_revoked", request("/v1/account", token=login["accessToken"])[0] == 401)
    check("old_refresh_replay_rejected", request("/v1/auth/refresh", {"refreshToken": login["refreshToken"]})[0] == 401)
    check("logout", request("/v1/auth/logout", {}, rotated["accessToken"])[0] == 204)
    check("logout_revokes_access", request("/v1/account", token=rotated["accessToken"])[0] == 401)
    check("logout_revokes_refresh", request("/v1/auth/refresh", {"refreshToken": rotated["refreshToken"]})[0] == 401)
    check("other_session_survives_logout", request("/v1/account", token=first["accessToken"])[0] == 200)

    restart()
    code, view, _ = request("/v1/account", token=first["accessToken"])
    check("session_survives_systemd_restart", code == 200 and view["playerId"] == first["playerId"])
    code, refreshed, _ = request("/v1/auth/refresh", {"refreshToken": second["refreshToken"]})
    check("refresh_survives_restart", code == 200 and refreshed["playerId"] == second["playerId"])
    remember(refreshed)
    check("absolute_session_expiry_preserved", refreshed["refreshExpiresAt"] == second["refreshExpiresAt"])

    # 同一数据库切换、回滚程序并清理本次验证版本，验证会话不依赖发布目录。
    original_release = pathlib.Path("/opt/project-star/api-current").resolve()
    tools = pathlib.Path(__file__).resolve().parent
    temporary_version = "p1-20261023-verify-" + uuid.uuid4().hex[:12]
    temporary_release = pathlib.Path("/opt/project-star/releases") / temporary_version
    run(["bash", str(tools / "InstallAccountHost.sh"), str(tools / "account-publish"), temporary_version])
    check("session_survives_program_update", request("/v1/account", token=first["accessToken"])[0] == 200)
    switch = "/opt/project-star/api-verify-rollback-" + uuid.uuid4().hex[:12]
    run(["sudo", "-n", "ln", "-s", str(original_release), switch])
    run(["sudo", "-n", "mv", "-Tf", switch, "/opt/project-star/api-current"])
    restart()
    check("session_survives_program_rollback", request("/v1/account", token=first["accessToken"])[0] == 200)
    if temporary_release.parent != pathlib.Path("/opt/project-star/releases") or temporary_release.is_symlink():
        raise AssertionError("unsafe_cleanup_target")
    run(["sudo", "-n", "rm", "-r", "--", str(temporary_release)])
    check("session_survives_old_program_cleanup", request("/v1/account", token=first["accessToken"])[0] == 200)
    if has_matches:
        check("match_states_survive_update_rollback_cleanup", sql(match_query) == original_matches)
    for index, query in enumerate(pvp_queries):
        check("pvp_data_survives_update_rollback_cleanup_" + str(index), sql(query) == original_pvp[index])

    new_password = password()
    admin("reset-password", prefix + "a", password=new_password + "\n")
    check("reset_revokes_all_sessions", request("/v1/account", token=first["accessToken"])[0] == 401)
    check("reset_revokes_refresh", request("/v1/auth/refresh", {"refreshToken": first["refreshToken"]})[0] == 401)
    check("old_password_rejected", request("/v1/auth/login", {"username": prefix + "a", "password": pwd})[0] == 401)
    code, changed, _ = request("/v1/auth/login", {"username": prefix + "a", "password": new_password})
    check("new_password_keeps_player_id", code == 200 and changed["playerId"] == first["playerId"])
    remember(changed)

    expiring = invite()
    digest = hashlib.sha256(expiring.encode()).hexdigest().upper()
    sql("UPDATE invitations SET expires_at=now()-interval '1 second' WHERE code_hash='" + digest + "'")
    check("expired_invite_rejected", request("/v1/auth/register", {"username": prefix + "expired", "password": pwd, "invitation": expiring})[0] == 400)
    once = invite()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(request, "/v1/auth/register", {"username": prefix + str(i), "password": pwd, "invitation": once}) for i in range(2)]
        results = [future.result() for future in futures]
    check("concurrent_invitation_one_winner", sorted(item[0] for item in results) == [201, 400])
    remember(next(item[1] for item in results if item[0] == 201))
    check("invalid_json_400", request("/v1/auth/login", raw=b"{")[0] == 400)
    check("oversized_body_413", request("/v1/auth/login", raw=b" " * 17000)[0] == 413)
    check("invalid_username_rejected", request("/v1/auth/register", {"username": "bad name", "password": pwd, "invitation": invite()})[0] == 400)
    check("short_password_rejected", request("/v1/auth/register", {"username": prefix + "short", "password": "short", "invitation": invite()})[0] == 400)

    access_hash = hashlib.sha256(changed["accessToken"].encode()).hexdigest().upper()
    sql("UPDATE auth_sessions SET access_expires_at=now()-interval '1 second' WHERE access_hash='" + access_hash + "'")
    check("expired_access_rejected", request("/v1/account", token=changed["accessToken"])[0] == 401)
    code, renewed, _ = request("/v1/auth/refresh", {"refreshToken": changed["refreshToken"]})
    check("refresh_after_access_expiry", code == 200)
    remember(renewed)
    refresh_hash = hashlib.sha256(renewed["refreshToken"].encode()).hexdigest().upper()
    sql("UPDATE auth_sessions SET refresh_expires_at=now()-interval '1 second' WHERE refresh_hash='" + refresh_hash + "'")
    check("absolute_expiry_rejects_refresh", request("/v1/auth/refresh", {"refreshToken": renewed["refreshToken"]})[0] == 401)
    check("absolute_expiry_rejects_access", request("/v1/account", token=renewed["accessToken"])[0] == 401)

    dump = run(["sudo", "-n", "-u", "project-star", "pg_dump", "--no-owner", DATABASE])
    journal = run(["sudo", "-n", "journalctl", "-u", "project-star-api", "--no-pager", "-o", "cat"])
    check("no_plaintext_credentials_in_database_or_log", all(value not in dump and value not in journal for value in SENSITIVE))
    check("passwords_use_identity_hash_format", sql("SELECT bool_and(password_hash LIKE 'AQAAAA%') FROM password_credentials") == "t")
    check("postgres_data_outside_program", run(["sudo", "-n", "-u", "postgres", "psql", "-Atqc", "SHOW data_directory"]).startswith("/var/lib/postgresql/"))
    release = pathlib.Path("/opt/project-star/api-current").resolve()
    writable = run(["sudo", "-n", "-u", "project-star", "find", str(release), "-writable", "-print", "-quit"])
    check("release_readonly_to_service", not writable)
    failed = subprocess.run(["sudo", "-n", "-u", "project-star", "env", "PROJECT_STAR_DATA_DIR=" + str(release), ENV[1], HOST, "migrate"], capture_output=True)
    check("program_directory_as_data_rejected", failed.returncode != 0)
    missing = subprocess.run(["sudo", "-n", "-u", "project-star", "env", "-u", "PROJECT_STAR_DATA_DIR", ENV[1], HOST, "migrate"], capture_output=True)
    check("missing_data_directory_rejected", missing.returncode != 0)

    # 独立数据库恢复，不覆盖现有账号。备份留在持久目录供后续离机备份。
    backup = "/var/lib/project-star/backups/" + prefix + ".dump"
    run(["sudo", "-n", "-u", "project-star", "mkdir", "-p", "-m", "0700", "/var/lib/project-star/backups"])
    run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, DATABASE])
    run(["sudo", "-n", "chmod", "0600", backup])
    restore_db = "project_star_restore_" + uuid.uuid4().hex[:12]
    run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restore_db])
    try:
        run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restore_db, backup])
        check("independent_backup_restore_players", sql("SELECT count(*) FROM players", restore_db) == sql("SELECT count(*) FROM players"))
        check("independent_backup_restore_sessions", sql("SELECT count(*) FROM auth_sessions", restore_db) == sql("SELECT count(*) FROM auth_sessions"))
        check("restored_session_identity", sql("SELECT player_id FROM auth_sessions WHERE refresh_hash='" + hashlib.sha256(refreshed["refreshToken"].encode()).hexdigest().upper() + "'", restore_db) == second["playerId"])
    finally:
        # 只删除本次明确创建的恢复验证库，绝不删除业务库。
        run(["sudo", "-n", "-u", "postgres", "dropdb", restore_db])

    statuses = [request("/v1/auth/login", {"username": prefix + "unknown", "password": pwd})[0] for _ in range(22)]
    check("login_rate_limit_429", 429 in statuses)
    check("ready_after_rate_limit", request("/health/ready")[0] == 200)
    report = {"date": "2026-10-23", "checks": len(PASSED), "passed": PASSED, "testAccountPrefix": prefix,
              "backup": backup, "release": str(release), "scope": "account-backend-only"}
    print(json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
