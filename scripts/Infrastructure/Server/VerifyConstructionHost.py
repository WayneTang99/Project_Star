#!/usr/bin/env python3
"""云端移动、出售、刷新及满盘领奖事务；只生成并修改本次qa账号。"""
import concurrent.futures
import copy
import hashlib
import json
import secrets
import sys
import uuid
import VerifyAccountHost as auth


def main():
    prefix = "qa_build_" + uuid.uuid4().hex[:9]
    invite = auth.invite(2)
    password = secrets.token_urlsafe(24)
    tokens, players, states, creates = [], [], [], []
    for suffix in ("a", "b"):
        code, session, _ = auth.request("/v1/auth/register", {"username": prefix + suffix, "password": password, "invitation": invite})
        auth.check("register_" + suffix, code == 201)
        tokens.append(session["accessToken"])
        players.append(session["playerId"])
        create = {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}
        code, state, _ = auth.request("/v1/matches", create, tokens[-1])
        auth.check("create_" + suffix, code == 201)
        states.append(state)
        creates.append(create)
    ids = [state["matchId"] for state in states]
    paths = ["/v1/matches/" + match_id + "/commands" for match_id in ids]

    def command(state, kind, **fields):
        return {"requestId": str(uuid.uuid4()), "expectedMatchVersion": state["version"], "type": kind, **fields}

    def submit(index, value):
        return auth.request(paths[index], value, tokens[index])

    def digest(index):
        return auth.sql("SELECT md5(state::text) || ':' || version FROM matches WHERE match_id='" + ids[index] + "'")

    def save(index):
        return json.loads(auth.sql("SELECT state::text FROM matches WHERE match_id='" + ids[index] + "'"))

    def together(index, values):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            return list(pool.map(lambda value: submit(index, value), values))

    legacy = {"requestId": "00000000-0000-0000-0000-000000000000", "action": "create", "matchId": None,
              "expectedVersion": None, "heroKey": "hero.paladin", "encounterKey": None, "offerIndex": None}
    expected = hashlib.sha256(json.dumps(legacy, separators=(",", ":")).encode()).hexdigest().upper()
    auth.check("new_optional_fields_keep_legacy_digest", auth.sql("SELECT request_hash FROM match_requests WHERE player_id='"
        + players[0] + "' AND request_id='" + creates[0]["requestId"] + "'") == expected)
    for index in range(2):
        choice = next(item for item in states[index]["state"]["choices"] if item["isCardShop"])
        code, states[index], _ = submit(index, command(states[index], "select_shop", encounterKey=choice["key"]))
        auth.check("enter_shop_" + str(index), code == 200)
        offer = next(item for item in states[index]["state"]["shop"]["offers"] if item["price"] <= states[index]["state"]["wealth"])
        code, states[index], _ = submit(index, command(states[index], "buy", offerIndex=offer["index"]))
        auth.check("buy_" + str(index), code == 200 and len(states[index]["state"]["cards"]) == 1)

    current = states[0]
    card = current["state"]["cards"][0]
    card_id = card["id"]
    other_hash = digest(1)
    move = command(current, "move_card", cardId=card_id, targetZone="Bench", targetStart=0)
    auth.check("move_requires_auth", auth.request(paths[0], move)[0] == 401)
    auth.check("move_cross_player_match_rejected", auth.request(paths[0], move, tokens[1])[0] == 404)
    before = digest(0)
    foreign_card = states[1]["state"]["cards"][0]["id"]
    auth.check("move_other_player_card_rejected", submit(0, {**move, "requestId": str(uuid.uuid4()), "cardId": foreign_card})[0] == 422)
    auth.check("sell_other_player_card_rejected", submit(0, command(current, "sell_card", cardId=foreign_card))[0] == 422)
    auth.check("move_missing_card_bad_request", submit(0, command(current, "move_card", targetZone="Bench", targetStart=0))[0] == 400)
    auth.check("move_bad_zone_bad_request", submit(0, {**move, "targetZone": "Other"})[0] == 400)
    auth.check("move_negative_start_bad_request", submit(0, {**move, "targetStart": -1})[0] == 400)
    auth.check("move_outside_start_bad_request", submit(0, {**move, "targetStart": 10})[0] == 400)
    auth.check("invalid_commands_keep_both_matches", digest(0) == before and digest(1) == other_hash)
    outcomes = together(0, [move, move])
    auth.check("concurrent_move_same_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    moved = outcomes[0][1]
    auth.check("move_one_version_same_economy", moved["version"] == current["version"] + 1
               and moved["state"]["wealth"] == current["state"]["wealth"] and moved["state"]["cards"][0]["id"] == card_id
               and moved["state"]["cards"][0]["zone"] == "Bench" and moved["state"]["cards"][0]["start"] == 0)
    auth.check("old_move_version_conflict", submit(0, {**move, "requestId": str(uuid.uuid4())})[1] == {"error": "version_conflict"})
    candidates = [command(moved, "move_card", cardId=card_id, targetZone=zone, targetStart=0) for zone in ("Battlefield", "Bench")]
    outcomes = together(0, candidates)
    auth.check("concurrent_different_moves_one_commit", sorted(item[0] for item in outcomes) == [200, 409])
    current = next(item[1] for item in outcomes if item[0] == 200)
    auth.restart()
    auth.check("move_restores_after_restart", auth.request("/v1/matches/current", token=tokens[0])[1] == current)
    auth.check("historical_move_retry_does_not_rewind", submit(0, move)[1] == moved
               and auth.request("/v1/matches/current", token=tokens[0])[1] == current)
    sale = command(current, "sell_card", cardId=card_id)
    auth.check("sale_missing_card_bad_request", submit(0, command(current, "sell_card"))[0] == 400)
    outcomes = together(0, [{**sale, "value": 999999, "wealth": 999999}] * 2)
    auth.check("concurrent_sale_same_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    sold = outcomes[0][1]
    auth.check("sale_uses_server_value_once", sold["version"] == current["version"] + 1 and not sold["state"]["cards"]
               and sold["state"]["wealth"] == current["state"]["wealth"] + current["state"]["cards"][0]["value"])
    current = sold
    sale_hash = digest(0)
    auth.check("sold_card_new_sale_rejected", submit(0, command(current, "sell_card", cardId=card_id))[0] == 422)
    auth.check("sold_card_cannot_move", submit(0, command(current, "move_card", cardId=card_id, targetZone="Bench", targetStart=0))[0] == 422)
    auth.check("rejected_resale_keeps_state", digest(0) == sale_hash)
    previous_keys = {item["key"] for item in current["state"]["shop"]["offers"]}
    auth.check("refresh_query_includes_cost_and_availability", current["state"]["shop"]["refreshCost"] == 2 and current["state"]["shop"]["canRefresh"])
    refreshes = [command(current, "refresh_shop") for _ in range(2)]
    outcomes = together(0, refreshes)
    auth.check("concurrent_refresh_one_commit", sorted(item[0] for item in outcomes) == [200, 409])
    winner = next(index for index, item in enumerate(outcomes) if item[0] == 200)
    refreshed = outcomes[winner][1]
    refresh = refreshes[winner]
    auth.check("refresh_debits_once_excludes_previous_keys", refreshed["version"] == current["version"] + 1
               and refreshed["state"]["wealth"] == current["state"]["wealth"] - 2
               and refreshed["state"]["shop"]["hasRefreshed"] and not refreshed["state"]["shop"]["canRefresh"]
               and all(item["key"] not in previous_keys for item in refreshed["state"]["shop"]["offers"]))
    current = refreshed
    refreshed_hash = digest(0)
    auth.check("second_refresh_rejected_without_debit", submit(0, command(current, "refresh_shop"))[0] == 422 and digest(0) == refreshed_hash)
    auth.check("refresh_retry_same_stock", submit(0, refresh)[1] == refreshed)
    auth.restart()
    auth.check("refresh_restores_after_restart", auth.request("/v1/matches/current", token=tokens[0])[1] == refreshed)
    auth.check("sale_retry_after_restart_same_original_receipt", submit(0, sale)[1] == sold and digest(0) == refreshed_hash)
    code, current, _ = submit(0, command(current, "leave_shop"))
    auth.check("refreshed_shop_can_leave", code == 200 and current["state"]["phase"] == "EncounterChoice")
    auth.check("refresh_after_leave_rejected", submit(0, command(current, "refresh_shop"))[1] == {"error": "match.wrong_phase"})
    auth.check("other_account_kept_original_shop", digest(1) == other_hash)

    # 第二个账号通过正常路径保存真实怪物结果，供本次满盘qa夹具保留完整对手。
    current_b = states[1]
    for turn in (1, 2, 3):
        code, current_b, _ = submit(1, command(current_b, "leave_shop"))
        auth.check("reward_path_leave_" + str(turn), code == 200 and current_b["state"]["turn"] == turn + 1)
        if turn < 3:
            choice = next(item for item in current_b["state"]["choices"] if item["isCardShop"])
            code, current_b, _ = submit(1, command(current_b, "select_shop", encounterKey=choice["key"]))
            auth.check("reward_path_shop_" + str(turn + 1), code == 200)
    code, prepared, _ = submit(1, command(current_b, "select_monster", encounterKey="monster.fire_slime"))
    auth.check("prepare_real_monster", code == 200)
    fixed_battle = prepared["state"]["battle"]
    code, current_b, _ = submit(1, command(prepared, "move_card", cardId=foreign_card, targetZone="Bench", targetStart=0))
    auth.check("prepare_move_keeps_opponent_identity", code == 200 and current_b["state"]["battle"] == fixed_battle)
    code, current_b, _ = submit(1, command(current_b, "resolve_monster", battleId=fixed_battle["id"]))
    auth.check("settle_after_prepare_move", code == 200 and current_b["state"]["phase"] == "MonsterResult")
    other_hash = digest(1)

    # 明确的qa满盘夹具：仅覆写本次账号a，卡牌模板取自实际固定怪物，非正常掉落。
    fixture = save(1)
    opponent = json.loads(fixture["battle"]["opponentSave"])
    template = next(item for item in opponent["cards"] if item["key"] == "card.lava_stone")
    placement_template = next(item for item in opponent["placements"] if item["placement"]["cardId"]["value"] == template["id"])
    fixture["matchId"] = ids[0]
    fixture["cards"], fixture["placements"] = [], []
    for field in ("skills", "setModifiers", "questModifiers"):
        fixture[field] = []
    fixture["pendingRewards"] = [{"kind": 0, "key": "card.fire_slime", "displayName": "火史莱姆", "level": 1}]
    for zone in (0, 1):
        for start in range(10):
            item = copy.deepcopy(template)
            item["id"] = str(uuid.uuid4())
            item["persistent"]["modifiers"] = []
            item["persistent"]["bases"] = [entry for entry in item["persistent"]["bases"] if entry["key"] != "Value"] + [{"key": "Value", "value": 5}]
            fixture["cards"].append(item)
            placement = copy.deepcopy(placement_template)
            placement["zone"] = zone
            placement["placement"].update({"cardId": {"value": item["id"]}, "start": start, "endExclusive": start + 1})
            fixture["placements"].append(placement)
    body = json.dumps(fixture, ensure_ascii=True, separators=(",", ":"))
    auth.sql("UPDATE matches SET state=$qa$" + body + "$qa$::jsonb,version=version+1 WHERE match_id='" + ids[0] + "' AND player_id='" + players[0] + "'")
    code, full, _ = auth.request("/v1/matches/current", token=tokens[0])
    auth.check("full_board_fixture_restores", code == 200 and len(full["state"]["cards"]) == 20)
    battle_id = full["state"]["battle"]["id"]
    claim = command(full, "claim_monster_reward", battleId=battle_id, rewardIndex=0)
    full_hash = digest(0)
    auth.check("full_board_claim_keeps_reward", submit(0, claim)[0] == 422 and digest(0) == full_hash)
    released = next(item for item in full["state"]["cards"] if item["zone"] == "Bench")
    blocked = command(full, "move_card", cardId=released["id"], targetZone="Battlefield", targetStart=0)
    auth.check("full_destination_move_keeps_source", submit(0, blocked)[0] == 422 and digest(0) == full_hash)
    sale = command(full, "sell_card", cardId=released["id"])
    outcomes = together(0, [sale, sale])
    auth.check("full_board_concurrent_sale_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    freed = outcomes[0][1]
    auth.check("sale_frees_one_slot_keeps_result_and_drop", len(freed["state"]["cards"]) == 19
               and freed["state"]["wealth"] == full["state"]["wealth"] + 5
               and freed["state"]["battle"] == full["state"]["battle"] and freed["state"]["pendingRewards"] == full["state"]["pendingRewards"])
    fresh_claim = command(freed, "claim_monster_reward", battleId=battle_id, rewardIndex=0)
    outcomes = together(0, [fresh_claim, fresh_claim])
    auth.check("claim_after_sale_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    claimed = outcomes[0][1]
    auth.check("claim_uses_freed_slot_once", len(claimed["state"]["cards"]) == 20 and not claimed["state"]["pendingRewards"]
               and sum(item["key"] == "card.fire_slime" for item in claimed["state"]["cards"]) == 1)
    claimed_hash = digest(0)
    auth.check("failed_old_claim_receipt_does_not_consume_again", submit(0, claim)[0] == 422 and digest(0) == claimed_hash)
    auth.restart()
    auth.check("claim_after_sale_survives_restart", auth.request("/v1/matches/current", token=tokens[0])[1] == claimed)
    auth.check("claim_retry_after_restart", submit(0, fresh_claim)[1] == claimed)
    code, finished, _ = submit(0, command(claimed, "finish_monster", battleId=battle_id))
    auth.check("full_board_reward_path_continues_turn_five", code == 200 and finished["state"]["turn"] == 5
               and finished["state"]["phase"] == "EncounterChoice")
    auth.check("fixture_never_changes_other_account", digest(1) == other_hash)

    backup = "/var/lib/project-star/backups/" + prefix + ".dump"
    final_hash = digest(0)
    auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
    auth.run(["sudo", "-n", "chmod", "0600", backup])
    restored_db = "project_star_restore_" + uuid.uuid4().hex[:12]
    auth.run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restored_db])
    try:
        auth.run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restored_db, backup])
        query = "SELECT md5(state::text) || ':' || version FROM matches WHERE match_id='" + ids[0] + "'"
        auth.check("backup_restores_construction_and_reward_state", auth.sql(query, restored_db) == final_hash)
        query = "SELECT md5(response::text) FROM match_requests WHERE player_id='" + players[0] + "' AND request_id='" + refresh["requestId"] + "'"
        auth.check("backup_restores_refresh_receipt", auth.sql(query, restored_db) == auth.sql(query))
    finally:
        auth.run(["sudo", "-n", "-u", "postgres", "dropdb", restored_db])
    print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED,
        "testAccountPrefix": prefix, "matchId": ids[0], "otherMatchId": ids[1], "backup": backup}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
