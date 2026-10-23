#!/usr/bin/env python3
"""云端第一条权威存档闭环；只操作本次 qa_ 测试玩家，不打印凭据。"""
import concurrent.futures
import hashlib
import json
import pathlib
import secrets
import sys
import uuid
import VerifyAccountHost as auth


def main():
    auth.restart()
    name = "qa_save_" + uuid.uuid4().hex[:10]
    invitation = auth.invite(2)
    password = secrets.token_urlsafe(24)
    sessions = []
    for suffix in ("a", "b"):
        code, session, _ = auth.request("/v1/auth/register", {"username": name + suffix, "password": password, "invitation": invitation})
        auth.check("registered_" + suffix, code == 201)
        sessions.append(session)
    token_a, token_b = [session["accessToken"] for session in sessions]
    create_id = str(uuid.uuid4())
    create = {"requestId": create_id, "heroKey": "hero.paladin"}
    auth.check("match_requires_auth", auth.request("/v1/matches", create)[0] == 401)
    auth.check("no_initial_match", auth.request("/v1/matches/current", token=token_a)[0] == 404)
    code, first, _ = auth.request("/v1/matches", create, token_a)
    auth.check("create_version_one", code == 201 and first["version"] == 1)
    auth.check("formal_economy_no_demo_wealth", first["state"]["wealth"] == 5 and first["state"]["experience"] == 1)
    auth.check("rules_version_present", len(first["rulesVersion"]) == 64)
    code, replay, _ = auth.request("/v1/matches", create, token_a)
    auth.check("create_retry_same_match", code == 201 and replay == first)
    auth.check("reused_create_id_rejected", auth.request("/v1/matches", {**create, "heroKey": "hero.mona"}, token_a)[0] == 409)
    auth.check("one_active_match", auth.request("/v1/matches", {**create, "requestId": str(uuid.uuid4())}, token_a)[1] == {"error": "match_in_progress"})
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: auth.request("/v1/matches", {**create, "requestId": str(uuid.uuid4())}, token_b), range(2)))
    auth.check("concurrent_creation_one_match", sorted(item[0] for item in outcomes) == [201, 409])
    other = next(item[1] for item in outcomes if item[0] == 201)
    auth.check("different_players_different_matches", other["matchId"] != first["matchId"])
    choice = next(item for item in first["state"]["choices"] if item["isCardShop"])
    command_path = "/v1/matches/" + first["matchId"] + "/commands"
    select = {"requestId": str(uuid.uuid4()), "expectedMatchVersion": 1, "type": "select_shop", "encounterKey": choice["key"]}
    auth.check("cross_player_command_rejected", auth.request(command_path, select, token_b)[0] == 404)
    auth.check("stale_version_rejected", auth.request(command_path, {**select, "expectedMatchVersion": 99}, token_a)[1] == {"error": "version_conflict"})
    requests = [{**select, "requestId": str(uuid.uuid4())} for _ in range(2)]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda value: auth.request(command_path, value, token_a), requests))
    auth.check("concurrent_version_one_commit", sorted(item[0] for item in outcomes) == [200, 409])
    selected_index = next(index for index, item in enumerate(outcomes) if item[0] == 200)
    selected = outcomes[selected_index][1]
    auth.check("select_advances_version", selected["version"] == 2 and selected["state"]["phase"] == "Shop")
    auth.check("select_retry_no_redraw", auth.request(command_path, requests[selected_index], token_a)[1] == selected)
    affordable = next(item for item in selected["state"]["shop"]["offers"] if item["price"] <= selected["state"]["wealth"])
    buy = {"requestId": str(uuid.uuid4()), "expectedMatchVersion": 2, "type": "buy", "offerIndex": affordable["index"]}
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: auth.request(command_path, buy, token_a), range(2)))
    auth.check("concurrent_retry_same_receipt", [item[0] for item in outcomes] == [200, 200] and outcomes[0][1] == outcomes[1][1])
    bought = outcomes[0][1]
    auth.check("one_purchase_one_debit", bought["version"] == 3 and len(bought["state"]["cards"]) == 1
               and bought["state"]["wealth"] == 5 - affordable["price"])
    auth.check("sold_offer_persisted", bought["state"]["shop"]["offers"][affordable["index"]]["sold"])
    auth.check("cannot_rebuy_sold_offer", auth.request(command_path, {**buy, "requestId": str(uuid.uuid4()), "expectedMatchVersion": 3}, token_a)[0] == 422)
    auth.check("reused_command_id_changed_payload", auth.request(command_path, {**buy, "offerIndex": (buy["offerIndex"] + 1) % 3}, token_a)[1] == {"error": "request_id_reused"})
    match_id = first["matchId"]
    query = "SELECT md5(state::text) || ':' || version FROM matches WHERE match_id='" + match_id + "'"
    before = auth.sql(query)
    for _ in range(3):
        auth.check("read_does_not_advance", auth.request("/v1/matches/current", token=token_a)[1] == bought)
    auth.check("reads_leave_database_identical", auth.sql(query) == before)
    auth.restart()
    auth.check("api_and_worker_restart_restore", auth.request("/v1/matches/current", token=token_a)[1] == bought)
    auth.check("retry_after_restart_same_receipt", auth.request(command_path, buy, token_a)[1] == bought)
    parent = auth.run(["sudo", "-n", "systemctl", "show", "project-star-api", "-p", "MainPID", "--value"])
    children = auth.run(["pgrep", "-P", parent]).splitlines()
    worker = next(pid for pid in children if pathlib.Path(auth.run(["sudo", "-n", "readlink", "/proc/" + pid + "/exe"])).name == "project-star-rules.x86_64")
    auth.run(["sudo", "-n", "kill", "-KILL", worker])
    after_crash = auth.request("/v1/matches/current", token=token_a)
    auth.check("crash_response_is_success_or_retryable", after_crash[0] in (200, 503))
    if after_crash[0] == 503:
        after_crash = auth.request("/v1/matches/current", token=token_a)
    auth.check("worker_crash_recovers_from_database", after_crash[0] == 200 and after_crash[1] == bought)
    auth.check("crash_leaves_database_identical", auth.sql(query) == before)
    auth.check("other_player_unchanged", auth.request("/v1/matches/current", token=token_b)[1] == other)
    relog = auth.request("/v1/auth/login", {"username": name + "a", "password": password})
    auth.check("relogin_restores_same_match", relog[0] == 200 and auth.request("/v1/matches/current", token=relog[1]["accessToken"])[1] == bought)
    auth.check("one_active_index_installed", auth.sql("SELECT count(*) FROM pg_indexes WHERE indexname='matches_one_active_player'") == "1")

    # 离店只能生成一次下一回合候选，旧商品和旧版本命令都不能继续操作。
    bought_save = json.loads(auth.sql("SELECT state::text FROM matches WHERE match_id='" + match_id + "'"))
    leave = {"requestId": str(uuid.uuid4()), "expectedMatchVersion": 3, "type": "leave_shop"}
    auth.check("leave_requires_auth", auth.request(command_path, leave)[0] == 401)
    auth.check("cross_player_leave_rejected", auth.request(command_path, leave, token_b)[0] == 404)
    auth.check("stale_leave_rejected", auth.request(command_path, {**leave, "requestId": str(uuid.uuid4()), "expectedMatchVersion": 2}, token_a)[1] == {"error": "version_conflict"})
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: auth.request(command_path, leave, token_a), range(2)))
    auth.check("concurrent_leave_one_receipt", [item[0] for item in outcomes] == [200, 200] and outcomes[0][1] == outcomes[1][1])
    left = outcomes[0][1]
    auth.check("leave_version_and_phase", left["version"] == 4 and left["state"]["phase"] == "EncounterChoice" and left["state"]["shop"] is None)
    auth.check("leave_no_turn_skip", left["state"]["round"] == 1 and left["state"]["turn"] == 2)
    auth.check("leave_one_experience_no_extra_income", left["state"]["experience"] == 2 and left["state"]["wealth"] == bought["state"]["wealth"])
    auth.check("leave_preserves_owned_cards", left["state"]["cards"] == bought["state"]["cards"])
    left_save = json.loads(auth.sql("SELECT state::text FROM matches WHERE match_id='" + match_id + "'"))
    auth.check("leave_settlement_cursors", left_save["incomeSettledThroughRound"] == 1 and left_save["experienceSettledThroughTurn"] == 2)
    auth.check("leave_keeps_encounter_history", set(bought_save["seenEncounters"]).issubset(left_save["seenEncounters"]))
    left_hash = auth.sql(query)
    code, rejection, _ = auth.request(command_path, {**leave, "requestId": str(uuid.uuid4()), "expectedMatchVersion": 4}, token_a)
    auth.check("second_leave_wrong_phase", code == 422 and rejection == {"error": "match.wrong_phase"})
    auth.check("old_shop_purchase_rejected", auth.request(command_path, {**buy, "requestId": str(uuid.uuid4()), "expectedMatchVersion": 4}, token_a)[1] == {"error": "match.wrong_phase"})
    auth.check("old_shop_version_conflict", auth.request(command_path, {**buy, "requestId": str(uuid.uuid4())}, token_a)[1] == {"error": "version_conflict"})
    auth.check("rejected_actions_do_not_reroll", auth.sql(query) == left_hash)
    auth.restart()
    auth.check("next_choices_survive_restart", auth.request("/v1/matches/current", token=token_a)[1] == left)
    auth.check("leave_retry_after_restart_same_receipt", auth.request(command_path, leave, token_a)[1] == left)
    auth.check("restart_and_retry_keep_random_state", auth.sql(query) == left_hash)
    current = left
    for turn in (2, 3):
        next_choice = next(item for item in current["state"]["choices"] if item["isCardShop"])
        next_select = {"requestId": str(uuid.uuid4()), "expectedMatchVersion": current["version"], "type": "select_shop", "encounterKey": next_choice["key"]}
        code, entered, _ = auth.request(command_path, next_select, token_a)
        auth.check("enter_subsequent_shop_" + str(turn), code == 200 and entered["state"]["phase"] == "Shop" and entered["version"] == current["version"] + 1)
        # 第二回合覆盖商店内重启；避免重复重启触发systemd的生产保护。
        if turn == 2:
            auth.restart()
            auth.check("subsequent_shop_survives_restart", auth.request("/v1/matches/current", token=token_a)[1] == entered)
        code, current, _ = auth.request(command_path, {"requestId": str(uuid.uuid4()), "expectedMatchVersion": entered["version"], "type": "leave_shop"}, token_a)
        auth.check("leave_subsequent_shop_" + str(turn), code == 200 and current["state"]["turn"] == turn + 1
                   and current["state"]["experience"] == turn + 1 and current["state"]["wealth"] == bought["state"]["wealth"])
    auth.check("turn_four_strict_monster_choices", len(current["state"]["choices"]) == 3 and all(item["kind"] == "Monster" for item in current["state"]["choices"]))
    final_hash = auth.sql(query)
    auth.check("old_leave_receipt_stays_original", auth.request(command_path, leave, token_a)[1] == left)
    auth.check("old_receipt_cannot_rewind_match", auth.sql(query) == final_hash and auth.request("/v1/matches/current", token=token_a)[1] == current)
    auth.check("other_player_unchanged_after_leave", auth.request("/v1/matches/current", token=token_b)[1] == other)
    before = final_hash

    backup = "/var/lib/project-star/backups/" + name + ".dump"
    auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
    auth.run(["sudo", "-n", "chmod", "0600", backup])
    restore_db = "project_star_restore_" + uuid.uuid4().hex[:12]
    auth.run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restore_db])
    try:
        auth.run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restore_db, backup])
        auth.check("backup_restore_match_state", auth.sql(query, restore_db) == before)
        receipt_query = "SELECT md5(response::text) FROM match_requests WHERE player_id='" + sessions[0]["playerId"] + "' AND request_id='" + buy["requestId"] + "'"
        auth.check("backup_restore_purchase_receipt", auth.sql(receipt_query, restore_db) == auth.sql(receipt_query))
    finally:
        auth.run(["sudo", "-n", "-u", "postgres", "dropdb", restore_db])
    print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED,
                      "testAccountPrefix": name, "backup": backup, "matchId": match_id}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
