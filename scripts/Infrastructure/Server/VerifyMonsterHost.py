#!/usr/bin/env python3
"""云端怪物战事务验证；正常商店路径及明确标注、仅本次qa账号的奖励夹具。"""
import concurrent.futures
import hashlib
import json
import secrets
import sys
import time
import uuid
import VerifyAccountHost as auth


def main():
    prefix = "qa_monster_" + uuid.uuid4().hex[:8]
    invitation = auth.invite(2)
    password = secrets.token_urlsafe(24)
    sessions = []
    for suffix in ("a", "b"):
        code, session, _ = auth.request("/v1/auth/register", {
            "username": prefix + suffix, "password": password, "invitation": invitation})
        auth.check("register_" + suffix, code == 201)
        sessions.append(session)
    token, other_token = [item["accessToken"] for item in sessions]
    create = {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}
    code, current, _ = auth.request("/v1/matches", create, token)
    auth.check("create_normal_match", code == 201)
    match_id = current["matchId"]
    path = "/v1/matches/" + match_id + "/commands"
    query = "SELECT md5(state::text) || ':' || version FROM matches WHERE match_id='" + match_id + "'"

    def command(kind, state=None, **fields):
        return {"requestId": str(uuid.uuid4()), "expectedMatchVersion": (state or current)["version"], "type": kind, **fields}

    def submit(value):
        return auth.request(path, value, token)

    # 验证增加BattleId等字段后，既有创建命令仍使用原摘要格式。
    legacy = {"requestId": "00000000-0000-0000-0000-000000000000", "action": "create", "matchId": None,
              "expectedVersion": None, "heroKey": "hero.paladin", "encounterKey": None, "offerIndex": None}
    expected_hash = hashlib.sha256(json.dumps(legacy, separators=(",", ":")).encode()).hexdigest().upper()
    auth.check("legacy_command_digest_unchanged", auth.sql("SELECT request_hash FROM match_requests WHERE player_id='"
        + sessions[0]["playerId"] + "' AND request_id='" + create["requestId"] + "'") == expected_hash)
    for turn in (1, 2, 3):
        choice = next(item for item in current["state"]["choices"] if item["isCardShop"])
        code, current, _ = submit(command("select_shop", encounterKey=choice["key"]))
        auth.check("normal_shop_" + str(turn), code == 200)
        if turn == 1:
            offer = next(item for item in current["state"]["shop"]["offers"] if item["price"] <= current["state"]["wealth"])
            code, current, _ = submit(command("buy", offerIndex=offer["index"]))
            auth.check("normal_purchase", code == 200 and len(current["state"]["cards"]) == 1)
        code, current, _ = submit(command("leave_shop"))
        auth.check("normal_leave_" + str(turn), code == 200 and current["state"]["turn"] == turn + 1)
    auth.check("strict_three_monsters", len(current["state"]["choices"]) == 3
               and all(item["kind"] == "Monster" for item in current["state"]["choices"]))
    before = auth.sql(query)
    invalid = command("select_monster", encounterKey="monster.missing")
    auth.check("unknown_monster_rejected", submit(invalid)[0] == 422 and auth.sql(query) == before)
    select = command("select_monster", encounterKey="monster.fire_slime")
    auth.check("select_requires_auth", auth.request(path, select)[0] == 401)
    auth.check("cross_player_select_rejected", auth.request(path, select, other_token)[0] == 404)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: submit(select), range(2)))
    prepared = outcomes[0][1]
    auth.check("concurrent_select_one_preparation", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    auth.check("prepare_fixed_battle_turn", prepared["version"] == current["version"] + 1
               and prepared["state"]["phase"] == "MonsterPreparation" and prepared["state"]["turn"] == 5
               and prepared["state"]["battle"]["turn"] == 4 and prepared["state"]["battle"]["result"] is None)
    current = prepared
    battle_id = current["state"]["battle"]["id"]
    prepare_hash = auth.sql(query)
    auth.check("cannot_redraw_prepared_monster", submit(command("select_monster", encounterKey="monster.fire_slime"))[0] == 422)
    auth.check("cannot_finish_before_settlement", submit(command("finish_monster", battleId=battle_id))[0] == 422)
    auth.check("missing_battle_id_bad_request", submit(command("resolve_monster"))[0] == 400)
    auth.check("wrong_battle_id_rejected", submit(command("resolve_monster", battleId=str(uuid.uuid4())))[1] == {"error": "match.battle_mismatch"})
    auth.check("invalid_actions_keep_preparation", auth.sql(query) == prepare_hash)
    auth.restart()
    auth.check("preparation_survives_full_restart", auth.request("/v1/matches/current", token=token)[1] == prepared)
    auth.check("select_retry_after_restart", submit(select)[1] == prepared)
    resolve = command("resolve_monster", battleId=battle_id)
    started = time.monotonic()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: submit(resolve), range(2)))
    elapsed = time.monotonic() - started
    settled = outcomes[0][1]
    auth.check("concurrent_settle_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    auth.check("result_persisted_once", settled["version"] == prepared["version"] + 1
               and settled["state"]["phase"] == "MonsterResult" and settled["state"]["battle"]["id"] == battle_id)
    result = settled["state"]["battle"]["result"]
    lost_health = 40 - min(40, max(0, result["opponentRemainingHealth"]))
    auth.check("formal_monster_damage_rewards", settled["state"]["wealth"] == prepared["state"]["wealth"] + 3 * lost_health // 40
               and settled["state"]["experience"] == prepared["state"]["experience"] + 2 * lost_health // 40
               and settled["state"]["reputation"] == prepared["state"]["reputation"])
    auth.check("drop_only_on_victory", len(settled["state"]["pendingRewards"]) == (1 if result["outcome"] == "PlayerVictory" else 0))
    current = settled
    settlement_json = auth.sql("SELECT state::text FROM matches WHERE match_id='" + match_id + "'")
    result_hash = auth.sql(query)
    auth.check("new_id_cannot_settle_twice", submit(command("resolve_monster", battleId=battle_id))[1] == {"error": "match.wrong_phase"}
               and auth.sql(query) == result_hash)
    auth.restart()
    auth.check("result_and_drop_survive_restart", auth.request("/v1/matches/current", token=token)[1] == settled)
    auth.check("settle_retry_after_restart", submit(resolve)[1] == settled and auth.sql(query) == result_hash)
    if current["state"]["pendingRewards"]:
        auth.check("unclaimed_drop_blocks_finish", submit(command("finish_monster", battleId=battle_id))[1] == {"error": "match.rewards_pending"})
        claim = command("claim_monster_reward", battleId=battle_id, rewardIndex=0)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            outcomes = list(pool.map(lambda _: submit(claim), range(2)))
        auth.check("concurrent_real_drop_claim_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
        current = outcomes[0][1]
        auth.check("real_drop_removed_once", not current["state"]["pendingRewards"])
        auth.check("cannot_reclaim_real_drop", submit(command("claim_monster_reward", battleId=battle_id, rewardIndex=0))[0] == 422)
    finish = command("finish_monster", battleId=battle_id)
    code, finished, _ = submit(finish)
    auth.check("continue_turn_five", code == 200 and finished["state"]["phase"] == "EncounterChoice"
               and finished["state"]["turn"] == 5 and finished["state"]["battle"] is None
               and finished["state"]["experience"] == settled["state"]["experience"] + 1)
    current = finished
    finished_hash = auth.sql(query)
    auth.check("cannot_finish_twice", submit(command("finish_monster", battleId=battle_id))[0] == 422)
    auth.check("old_settle_receipt_cannot_rewind", submit(resolve)[1] == settled and auth.sql(query) == finished_hash
               and auth.request("/v1/matches/current", token=token)[1] == finished)

    # 单独qa对局的待领奖励夹具：明确只改本次新建的测试玩家，验证两个领取分支。
    code, fixture, _ = auth.request("/v1/matches", {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}, other_token)
    auth.check("create_reward_fixture_account", code == 201)
    fixture_id = fixture["matchId"]
    # 复制实际结算存档的固定对手与结果，仅清空本次qa夹具库存并补待领项。
    fixture_save = json.loads(settlement_json)
    fixture_save["matchId"] = fixture_id
    for field in ("cards", "skills", "placements", "setModifiers", "questModifiers"):
        fixture_save[field] = []
    fixture_save["pendingRewards"] = [{"kind": 0, "key": "card.silver_needle_grass", "displayName": "银针草", "level": 1}]

    def install_fixture(save):
        body = json.dumps(save, ensure_ascii=True, separators=(",", ":"))
        auth.sql("UPDATE matches SET state=$qa$" + body + "$qa$::jsonb,version=version+1 WHERE match_id='"
                 + fixture_id + "' AND player_id='" + sessions[1]["playerId"] + "'")
        return auth.request("/v1/matches/current", token=other_token)[1]

    fixture = install_fixture(fixture_save)
    fixture_path = "/v1/matches/" + fixture_id + "/commands"
    fixture_battle = fixture["state"]["battle"]["id"]
    claim = command("claim_monster_reward", fixture, battleId=fixture_battle, rewardIndex=0)
    auth.check("reward_cross_player_rejected", auth.request(fixture_path, claim, token)[0] == 404)
    auth.check("reward_wrong_battle_rejected", auth.request(fixture_path, {**claim, "requestId": str(uuid.uuid4()), "battleId": str(uuid.uuid4())}, other_token)[0] == 422)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes = list(pool.map(lambda _: auth.request(fixture_path, claim, other_token), range(2)))
    auth.check("card_reward_concurrent_claim_once", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    claimed = outcomes[0][1]
    auth.check("card_reward_added_and_removed", not claimed["state"]["pendingRewards"] and len(claimed["state"]["cards"]) == len(fixture["state"]["cards"]) + 1)
    auth.check("card_reward_reclaim_rejected", auth.request(fixture_path, command("claim_monster_reward", claimed, battleId=fixture_battle, rewardIndex=0), other_token)[0] == 422)
    fixture_save["pendingRewards"] = [{"kind": 1, "key": "skill.burn_mastery", "displayName": "灼伤精通", "level": 1}]
    fixture = install_fixture(fixture_save)
    skill_claim = command("claim_monster_reward", fixture, battleId=fixture_battle, rewardIndex=0)
    code, skill_claimed, _ = auth.request(fixture_path, skill_claim, other_token)
    auth.check("skill_reward_added_and_removed", code == 200 and not skill_claimed["state"]["pendingRewards"]
               and any(item["key"] == "skill.burn_mastery" for item in skill_claimed["state"]["skills"]))
    auth.restart()
    auth.check("claimed_skill_survives_restart", auth.request("/v1/matches/current", token=other_token)[1] == skill_claimed)
    auth.check("reward_retry_after_restart", auth.request(fixture_path, skill_claim, other_token)[1] == skill_claimed)
    auth.check("other_match_unchanged_by_fixture", auth.sql(query) == finished_hash)

    backup = "/var/lib/project-star/backups/" + prefix + ".dump"
    auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
    auth.run(["sudo", "-n", "chmod", "0600", backup])
    restore_db = "project_star_restore_" + uuid.uuid4().hex[:12]
    auth.run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restore_db])
    try:
        auth.run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restore_db, backup])
        auth.check("backup_restores_completed_monster", auth.sql(query, restore_db) == finished_hash)
        receipt_query = "SELECT md5(response::text) FROM match_requests WHERE player_id='" + sessions[0]["playerId"] + "' AND request_id='" + resolve["requestId"] + "'"
        auth.check("backup_restores_settlement_receipt", auth.sql(receipt_query, restore_db) == auth.sql(receipt_query))
    finally:
        auth.run(["sudo", "-n", "-u", "postgres", "dropdb", restore_db])
    print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED,
        "testAccountPrefix": prefix, "matchId": match_id, "fixtureMatchId": fixture_id,
        "outcome": result["outcome"], "settleConcurrentSeconds": round(elapsed, 3),
        "resultResponseBytes": len(json.dumps(settled).encode()), "backup": backup}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
