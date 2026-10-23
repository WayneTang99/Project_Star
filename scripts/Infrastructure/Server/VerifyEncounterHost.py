#!/usr/bin/env python3
"""正常首轮到PvP候选；独立qa账号覆盖指定事件与后续轮次探险，不打印存档或凭据。"""
import concurrent.futures
import copy
import json
import secrets
import sys
import uuid
import VerifyAccountHost as auth


def main():
    prefix = "qa_event_" + uuid.uuid4().hex[:9]
    invitation = auth.invite(2)
    password = secrets.token_urlsafe(24)
    tokens, players, states = [], [], []
    for suffix in ("a", "b"):
        code, account, _ = auth.request("/v1/auth/register", {"username": prefix + suffix, "password": password, "invitation": invitation})
        auth.check("register_" + suffix, code == 201)
        tokens.append(account["accessToken"])
        players.append(account["playerId"])
        code, current, _ = auth.request("/v1/matches", {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}, tokens[-1])
        auth.check("create_" + suffix, code == 201)
        states.append(current)
    ids = [state["matchId"] for state in states]
    paths = ["/v1/matches/" + match_id + "/commands" for match_id in ids]

    def command(state, kind, **fields):
        return {"requestId": str(uuid.uuid4()), "expectedMatchVersion": state["version"], "type": kind, **fields}

    def submit(index, body):
        return auth.request(paths[index], body, tokens[index])

    def digest(index, database="project_star"):
        return auth.sql("SELECT md5(state::text) || ':' || version FROM matches WHERE match_id='" + ids[index] + "'", database)

    def saved(index):
        return json.loads(auth.sql("SELECT state::text FROM matches WHERE match_id='" + ids[index] + "'"))

    def together(index, values):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            return list(pool.map(lambda value: submit(index, value), values))

    # 只为本次b账号注入明确的已下发候选，固定覆盖各事件；a账号始终走正常排程。
    baseline = saved(1)

    def fixture(key, level=1, round_number=1, wealth=5, random_state=None):
        data = copy.deepcopy(baseline)
        data.update({"round": round_number, "turn": 1, "incomeSettledThroughRound": round_number,
                     "experienceSettledThroughTurn": (round_number - 1) * 8 + 1})
        data["choices"] = [{"key": key, "displayName": "事件验证夹具", "kind": 3, "level": level}]
        if random_state is not None:
            data["randomState"] = str(random_state)
        for item in data["resources"]["bases"]:
            if item["key"] == "Wealth":
                item["value"] = wealth
        body = json.dumps(data, ensure_ascii=True, separators=(",", ":"))
        auth.sql("UPDATE matches SET state=$qa$" + body + "$qa$::jsonb,version=version+1 WHERE match_id='" + ids[1]
                 + "' AND player_id='" + players[1] + "'")
        status, state, _ = auth.request("/v1/matches/current", token=tokens[1])
        if status != 200:
            raise AssertionError("event_fixture_restore")
        return state

    a_initial = digest(0)
    current = fixture("encounter.landfill")
    select = command(current, "select_event", encounterKey="encounter.landfill")
    auth.check("event_select_requires_auth", auth.request(paths[1], select)[0] == 401)
    auth.check("event_select_cross_player_rejected", auth.request(paths[1], select, tokens[0])[0] == 404)
    auth.check("event_select_missing_key_bad_request", submit(1, command(current, "select_event"))[0] == 400)
    auth.check("event_select_unissued_key_rejected", submit(1, {**select, "requestId": str(uuid.uuid4()), "encounterKey": "encounter.tavern"})[0] == 422)
    outcomes = together(1, [select, select])
    auth.check("concurrent_event_select_one_visit", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    opened = outcomes[0][1]
    visit_id = opened["state"]["eventVisit"]["id"]
    auth.check("event_open_fixed_slots_and_boundary", opened["version"] == current["version"] + 1
               and opened["state"]["phase"] == "Event" and opened["state"]["turn"] == 2
               and opened["state"]["eventVisit"]["turn"] == 1 and len(opened["state"]["eventVisit"]["options"]) == 2
               and not opened["state"]["eventVisit"]["resolved"])
    before = digest(1)
    pending_leave = command(opened, "leave_event", visitId=visit_id)
    auth.check("unresolved_event_cannot_leave", submit(1, pending_leave)[1] == {"error": "match.event_pending"})
    resolve = command(opened, "resolve_event", visitId=visit_id, optionKey="encounter.landfill.wealth")
    auth.check("resolve_missing_visit_bad_request", submit(1, command(opened, "resolve_event", optionKey="encounter.landfill.wealth"))[0] == 400)
    auth.check("resolve_missing_option_bad_request", submit(1, command(opened, "resolve_event", visitId=visit_id))[0] == 400)
    auth.check("resolve_wrong_visit_rejected", submit(1, {**resolve, "requestId": str(uuid.uuid4()), "visitId": str(uuid.uuid4())})[1] == {"error": "match.visit_mismatch"})
    auth.check("resolve_unissued_option_rejected", submit(1, {**resolve, "requestId": str(uuid.uuid4()), "optionKey": "option.missing"})[0] == 422)
    auth.check("event_rejections_keep_original_state", digest(1) == before and digest(0) == a_initial)
    auth.restart()
    auth.check("event_options_survive_restart", auth.request("/v1/matches/current", token=tokens[1])[1] == opened)
    auth.check("event_select_retry_after_restart", submit(1, select)[1] == opened)
    outcomes = together(1, [resolve, resolve])
    auth.check("concurrent_event_resolution_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    resolved = outcomes[0][1]
    auth.check("one_wealth_gain_and_saved_summary", resolved["version"] == opened["version"] + 1
               and resolved["state"]["wealth"] == opened["state"]["wealth"] + 2
               and resolved["state"]["eventVisit"]["result"]["wealthGained"] == 2 and resolved["state"]["eventVisit"]["resolved"])
    result_hash = digest(1)
    auth.check("new_id_cannot_resolve_twice", submit(1, command(resolved, "resolve_event", visitId=visit_id, optionKey="encounter.landfill.wealth"))[0] == 422)
    auth.check("changed_option_same_id_rejected", submit(1, {**resolve, "optionKey": "encounter.landfill.material_small_card"})[1] == {"error": "request_id_reused"})
    auth.check("repeat_resolution_keeps_state", digest(1) == result_hash)
    auth.restart()
    auth.check("event_result_survives_restart", auth.request("/v1/matches/current", token=tokens[1])[1] == resolved)
    auth.check("result_retry_after_restart", submit(1, resolve)[1] == resolved)
    leave = command(resolved, "leave_event", visitId=visit_id)
    outcomes = together(1, [leave, leave])
    auth.check("concurrent_leave_one_receipt", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
    left = outcomes[0][1]
    auth.check("event_leave_no_extra_turn_or_income", left["state"]["phase"] == "EncounterChoice" and left["state"]["eventVisit"] is None
               and left["state"]["turn"] == 2 and left["state"]["experience"] == 2 and left["state"]["wealth"] == resolved["state"]["wealth"])
    left_hash = digest(1)
    auth.check("old_failed_leave_receipt_stays_failed", submit(1, pending_leave)[0] == 422)
    auth.check("old_event_receipt_cannot_rewind", submit(1, resolve)[1] == resolved and digest(1) == left_hash)

    current = fixture("encounter.landfill")
    code, current, _ = submit(1, command(current, "select_event", encounterKey="encounter.landfill"))
    auth.check("revisit_has_new_visit_identity", code == 200 and current["state"]["eventVisit"]["id"] != visit_id)
    auth.check("old_visit_cannot_target_new_visit", submit(1, command(current, "resolve_event", visitId=visit_id, optionKey="encounter.landfill.wealth"))[0] == 422)
    candidates = [command(current, "resolve_event", visitId=current["state"]["eventVisit"]["id"], optionKey=option["key"])
                  for option in current["state"]["eventVisit"]["options"]]
    outcomes = together(1, candidates)
    auth.check("different_options_same_version_one_commit", sorted(item[0] for item in outcomes) == [200, 409])
    current = fixture("encounter.tavern", wealth=0)
    code, current, _ = submit(1, command(current, "select_event", encounterKey="encounter.tavern"))
    auth.check("tavern_open", code == 200)
    poor = digest(1)
    auth.check("tavern_trade_insufficient_wealth_keeps_visit", submit(1, command(current, "resolve_event", visitId=current["state"]["eventVisit"]["id"], optionKey="encounter.tavern.trade"))[0] == 422
               and digest(1) == poor)
    code, current, _ = submit(1, command(current, "resolve_event", visitId=current["state"]["eventVisit"]["id"], optionKey="encounter.tavern.cheers"))
    auth.check("tavern_health_bonus_visible_and_persistent", code == 200 and current["state"]["pendingBattleMaxHealthBonus"] == 10
               and current["state"]["eventVisit"]["result"]["pendingBattleMaxHealthBonus"] == 10)
    auth.check("fixture_did_not_modify_normal_account", digest(0) == a_initial)

    # a账号从正常候选走完首轮，不注入seed、候选、资金或数值。
    current = states[0]
    event_count = 0
    while current["state"]["turn"] < 8:
        turn = current["state"]["turn"]
        choice = next((item for item in current["state"]["choices"] if item["isChoiceEvent"]), current["state"]["choices"][0])
        if choice["isChoiceEvent"]:
            code, current, _ = submit(0, command(current, "select_event", encounterKey=choice["key"]))
            auth.check("normal_event_open_" + str(turn), code == 200)
            visit = current["state"]["eventVisit"]
            option = next(item for item in visit["options"] if not item["key"].endswith(".trade"))
            code, current, _ = submit(0, command(current, "resolve_event", visitId=visit["id"], optionKey=option["key"]))
            auth.check("normal_event_resolve_" + str(turn), code == 200)
            code, current, _ = submit(0, command(current, "leave_event", visitId=visit["id"]))
            auth.check("normal_event_leave_" + str(turn), code == 200 and current["state"]["turn"] == turn + 1)
            event_count += 1
        elif choice["kind"] == "Monster":
            code, current, _ = submit(0, command(current, "select_monster", encounterKey=choice["key"]))
            auth.check("normal_monster_open", code == 200)
            battle_id = current["state"]["battle"]["id"]
            code, current, _ = submit(0, command(current, "resolve_monster", battleId=battle_id))
            auth.check("normal_monster_settle", code == 200)
            while current["state"]["pendingRewards"]:
                code, current, _ = submit(0, command(current, "claim_monster_reward", battleId=battle_id, rewardIndex=0))
                auth.check("normal_monster_reward", code == 200)
            code, current, _ = submit(0, command(current, "finish_monster", battleId=battle_id))
            auth.check("normal_monster_finish", code == 200)
        else:
            code, current, _ = submit(0, command(current, "select_shop", encounterKey=choice["key"]))
            auth.check("normal_shop_open_" + str(turn), code == 200)
            code, current, _ = submit(0, command(current, "leave_shop"))
            auth.check("normal_shop_leave_" + str(turn), code == 200)
    auth.check("normal_path_reaches_strict_turn_eight_pvp", current["state"]["round"] == 1 and len(current["state"]["choices"]) == 1
               and current["state"]["choices"][0]["kind"] == "Pvp")
    normal_hash = digest(0)

    # 后续轮次明确的探险夹具，寻找正式加权效果中的怪物分支，不提供HTTP指定结果功能。
    exploration = None
    redirect_request = None
    for seed in range(1, 65):
        current_b = fixture("encounter.quicksand_expanse", level=2, round_number=3, random_state=seed)
        code, current_b, _ = submit(1, command(current_b, "select_event", encounterKey="encounter.quicksand_expanse"))
        if code != 200:
            raise AssertionError("exploration_open")
        request = command(current_b, "resolve_event", visitId=current_b["state"]["eventVisit"]["id"], optionKey="encounter.quicksand_expanse.cross_sandstorm")
        code, candidate, _ = submit(1, request)
        if code == 200 and candidate["state"]["phase"] == "MonsterPreparation":
            exploration, redirect_request = candidate, request
            break
    auth.check("exploration_rolls_actual_monster_branch", exploration is not None)
    battle = exploration["state"]["battle"]
    auth.check("event_monster_preserves_original_turn", exploration["state"]["turn"] == 2 and battle["turn"] == 1
               and battle["round"] == 3 and battle["sourceEncounterKey"] == "encounter.quicksand_expanse" and exploration["state"]["eventVisit"] is None)
    auth.restart()
    auth.check("event_monster_survives_restart", auth.request("/v1/matches/current", token=tokens[1])[1] == exploration)
    auth.check("event_redirect_retry_keeps_same_monster", submit(1, redirect_request)[1] == exploration)
    code, settled, _ = submit(1, command(exploration, "resolve_monster", battleId=battle["id"]))
    auth.check("event_monster_settles_after_restore", code == 200 and settled["state"]["phase"] == "MonsterResult")
    current_b = settled
    while current_b["state"]["pendingRewards"]:
        code, current_b, _ = submit(1, command(current_b, "claim_monster_reward", battleId=battle["id"], rewardIndex=0))
        auth.check("event_monster_reward", code == 200)
    code, current_b, _ = submit(1, command(current_b, "finish_monster", battleId=battle["id"]))
    auth.check("event_monster_finish_without_turn_skip", code == 200 and current_b["state"]["round"] == 3 and current_b["state"]["turn"] == 2)
    auth.check("exploration_fixture_keeps_normal_account", digest(0) == normal_hash)

    backup = "/var/lib/project-star/backups/" + prefix + ".dump"
    auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
    auth.run(["sudo", "-n", "chmod", "0600", backup])
    restored_db = "project_star_restore_" + uuid.uuid4().hex[:12]
    auth.run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restored_db])
    try:
        auth.run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restored_db, backup])
        auth.check("backup_restores_turn_eight", digest(0, restored_db) == normal_hash)
        auth.check("backup_restores_event_monster", digest(1, restored_db) == digest(1))
        query = "SELECT md5(response::text) FROM match_requests WHERE player_id='" + players[1] + "' AND request_id='" + redirect_request["requestId"] + "'"
        auth.check("backup_restores_event_resolution_receipt", auth.sql(query, restored_db) == auth.sql(query))
    finally:
        auth.run(["sudo", "-n", "-u", "postgres", "dropdb", restored_db])
    print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED, "testAccountPrefix": prefix,
        "normalMatchId": ids[0], "normalEventCount": event_count, "fixtureMatchId": ids[1], "backup": backup}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
