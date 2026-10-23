#!/usr/bin/env python3
"""真实账号快照PvP、租约故障与原子回滚；修改范围仅本次qa玩家和专用夹具行。"""
import concurrent.futures
import copy
import json
import pathlib
import secrets
import sys
import time
import uuid
import VerifyAccountHost as auth


def main():
    prefix = "qa_pvp_" + uuid.uuid4().hex[:9]
    invitation = auth.invite(5)
    password = secrets.token_urlsafe(24)
    tokens, players, states = [], [], []
    for suffix in "abcde":
        code, account, _ = auth.request("/v1/auth/register", {"username": prefix + suffix, "password": password, "invitation": invitation})
        auth.check("register_" + suffix, code == 201)
        tokens.append(account["accessToken"])
        players.append(account["playerId"])
        code, state, _ = auth.request("/v1/matches", {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}, tokens[-1])
        auth.check("create_" + suffix, code == 201)
        states.append(state)
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

    def parallel(index, body):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            return list(pool.map(lambda _: submit(index, body), range(2)))

    def wait_battle(index, battle_id, wanted="completed"):
        for _ in range(120):
            code, battle, _ = auth.request("/v1/battles/" + battle_id, token=tokens[index])
            if code == 200 and battle["status"] == wanted:
                return battle
            if code != 200 or (battle["status"] == "failed" and wanted != "failed"):
                raise AssertionError("unexpected_battle_state")
            time.sleep(0.25)
        raise AssertionError("battle_wait_timeout")

    def release(battle_id):
        auth.sql("UPDATE pvp_battles SET next_attempt_at=now() WHERE battle_id='" + battle_id + "' AND status='pending'")

    def normal_to_pvp(index):
        current = states[index]
        while current["state"]["turn"] < 8:
            turn = current["state"]["turn"]
            choices = current["state"]["choices"]
            choice = next((item for item in choices if turn >= 5 and item["isChoiceEvent"]),
                          next((item for item in choices if item["isCardShop"]), choices[0]))
            if choice["kind"] == "Monster":
                choice = next((item for item in choices if item["key"] == "monster.fire_slime"), choice)
                code, current, _ = submit(index, command(current, "select_monster", encounterKey=choice["key"]))
                if code != 200:
                    raise AssertionError("normal_monster_select")
                battle_id = current["state"]["battle"]["id"]
                code, current, _ = submit(index, command(current, "resolve_monster", battleId=battle_id))
                if code != 200:
                    raise AssertionError("normal_monster_resolve")
                while current["state"]["pendingRewards"]:
                    code, current, _ = submit(index, command(current, "claim_reward", battleId=battle_id, rewardIndex=0))
                    if code != 200:
                        raise AssertionError("normal_monster_reward")
                code, current, _ = submit(index, command(current, "finish_monster", battleId=battle_id))
            elif choice["isChoiceEvent"]:
                code, current, _ = submit(index, command(current, "select_event", encounterKey=choice["key"]))
                if code != 200:
                    raise AssertionError("normal_event_select")
                visit = current["state"]["eventVisit"]
                option = next(item for item in visit["options"] if not item["key"].endswith(".trade"))
                code, current, _ = submit(index, command(current, "resolve_event", visitId=visit["id"], optionKey=option["key"]))
                if code != 200:
                    raise AssertionError("normal_event_resolve")
                code, current, _ = submit(index, command(current, "leave_event", visitId=visit["id"]))
            else:
                code, current, _ = submit(index, command(current, "select_shop", encounterKey=choice["key"]))
                if code != 200:
                    raise AssertionError("normal_shop_select")
                if turn == 1:
                    offer = next(item for item in current["state"]["shop"]["offers"] if item["price"] <= current["state"]["wealth"])
                    code, current, _ = submit(index, command(current, "buy", offerIndex=offer["index"]))
                    auth.check("normal_purchase_" + str(index), code == 200)
                code, current, _ = submit(index, command(current, "leave_shop"))
            if code != 200:
                raise AssertionError("normal_turn_finish")
        auth.check("normal_turn_eight_" + str(index), current["state"]["round"] == 1 and len(current["state"]["choices"]) == 1
                   and current["state"]["choices"][0]["kind"] == "Pvp")
        states[index] = current
        return current

    def remap_snapshot(original, match_id):
        mapping = {}
        def visit(value):
            if isinstance(value, dict):
                return {key: visit(item) for key, item in value.items()}
            if isinstance(value, list):
                return [visit(item) for item in value]
            if isinstance(value, str):
                try:
                    uuid.UUID(value)
                    return mapping.setdefault(value, str(uuid.uuid4()))
                except ValueError:
                    return value
            return value
        result = visit(copy.deepcopy(original))
        result["matchId"] = match_id
        return result

    def fixture(index, original, wins=0, boost=False):
        data = remap_snapshot(original, ids[index])
        data["pvpWins"] = wins
        data["pendingRewards"] = []
        if boost:
            card_id = str(uuid.uuid4())
            data["cards"] = [{"id": card_id, "key": "card.assassin_dagger", "level": 1,
                "persistent": {"bases": [{"key": "Level", "value": 1}, {"key": "Value", "value": 2}], "modifiers": []},
                "combat": {"bases": [{"key": "AttackDamage", "value": 10000}, {"key": "CooldownTicks", "value": 10}], "modifiers": []},
                "cooldownMultiplier": 1, "quests": [], "gems": [None]}]
            data["placements"] = [{"zone": 0, "placement": {"cardId": {"value": card_id}, "size": 1, "start": 0, "isPushable": True}}]
            data["setModifiers"], data["questModifiers"] = [], []
            for item in data["hero"]["combat"]["bases"]:
                if item["key"] == "MaxHealth":
                    item["value"] = 10000
        body = json.dumps(data, ensure_ascii=True, separators=(",", ":"))
        auth.sql("UPDATE matches SET state=$qa$" + body + "$qa$::jsonb,version=version+1 WHERE match_id='" + ids[index]
                 + "' AND player_id='" + players[index] + "'")
        code, state, _ = auth.request("/v1/matches/current", token=tokens[index])
        if code != 200:
            raise AssertionError("pvp_fixture_restore")
        return state

    # 测试触发器仅延后本次qa玩家的新任务，保证故障注入可重复；finally中删除。
    trigger_name = "qa_hold_" + uuid.uuid4().hex[:12]
    player_list = ",".join("'" + item + "'" for item in players)
    auth.sql("CREATE FUNCTION " + trigger_name + "() RETURNS trigger LANGUAGE plpgsql AS $fn$ BEGIN IF NEW.player_id IN ("
             + player_list + ") THEN NEW.next_attempt_at=now()+interval '1 hour'; END IF; RETURN NEW; END $fn$; CREATE TRIGGER "
             + trigger_name + " BEFORE INSERT ON pvp_battles FOR EACH ROW EXECUTE FUNCTION " + trigger_name + "()")
    stopped_worker = None
    fake_snapshots = []
    try:
        auth.check("pvp_wrong_phase_rejected", submit(2, command(states[2], "start_pvp", encounterKey="encounter.pvp"))[0] == 422)
        first = normal_to_pvp(0)
        original = saved(0)
        version = first["version"]
        rules_version = first["rulesVersion"]
        auth.check("fresh_rule_pool_for_empty_pool_test", auth.sql("SELECT count(*) FROM pvp_snapshots WHERE rules_version='" + rules_version + "' AND round=1") == "0")
        # 只有不兼容版本／不同轮的明确夹具时仍应补位，不跨版本或跨轮匹配。
        for index, round_number, fingerprint in ((2, 1, "F" * 64), (3, 2, rules_version)):
            snapshot = remap_snapshot(original, ids[index])
            snapshot["round"] = round_number
            snapshot["rulesVersion"] = fingerprint
            snapshot_id = str(uuid.uuid4())
            fake_snapshots.append(snapshot_id)
            auth.sql("INSERT INTO pvp_snapshots(snapshot_id,player_id,match_id,round,rules_version,state) VALUES ('" + snapshot_id
                     + "','" + players[index] + "','" + ids[index] + "'," + str(round_number) + ",'" + fingerprint
                     + "',$qa$" + json.dumps(snapshot) + "$qa$)")
        start = command(first, "start_pvp", encounterKey="encounter.pvp")
        auth.check("pvp_start_requires_auth", auth.request(paths[0], start)[0] == 401)
        auth.check("pvp_cross_player_rejected", auth.request(paths[0], start, tokens[1])[0] == 404)
        auth.check("client_cannot_submit_pvp_result", submit(0, {**start, "type": "resolve_pvp"})[0] == 400)
        outcomes = parallel(0, start)
        auth.check("concurrent_start_one_receipt", all(item[0] == 202 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
        pending = outcomes[0][1]
        battle_a = pending["state"]["pvp"]["id"]
        auth.check("empty_pool_fixed_marked_test_opponent", pending["version"] == version + 1 and pending["state"]["phase"] == "PvpPending"
                   and pending["state"]["pvp"]["isTestOpponent"] and pending["state"]["pvp"]["opponentSnapshotId"] is None)
        for snapshot_id in fake_snapshots:
            auth.sql("DELETE FROM pvp_snapshots WHERE snapshot_id='" + snapshot_id + "'")
        fake_snapshots.clear()
        battle_path = "/v1/battles/" + battle_a
        auth.check("battle_read_requires_auth", auth.request(battle_path)[0] == 401)
        auth.check("battle_cross_player_read_rejected", auth.request(battle_path, token=tokens[1])[0] == 404)
        auth.check("pending_job_persisted", auth.request(battle_path, token=tokens[0])[1]["status"] == "pending")
        frozen_hash = digest(0)
        auth.check("pending_match_cannot_edit", submit(0, command(pending, "move_card", cardId=str(uuid.uuid4()), targetZone="Bench", targetStart=0))[1] == {"error": "match.wrong_phase"})
        auth.check("new_request_cannot_rematch", submit(0, command(pending, "start_pvp", encounterKey="encounter.pvp"))[0] == 422 and digest(0) == frozen_hash)
        auth.check("ignored_client_seed_cannot_change_receipt", submit(0, {**start, "seed": "attacker-seed", "opponentSnapshotId": str(uuid.uuid4())})[1] == pending)
        release(battle_a)
        completed_a = wait_battle(0, battle_a)
        result_a = completed_a["result"]
        auth.check("background_completion_one_version", result_a["version"] == version + 2 and result_a["state"]["phase"] == "PvpResult")
        outcome = result_a["state"]["pvp"]["result"]["outcome"]
        auth.check("pvp_formal_win_or_reputation_rule", result_a["state"]["pvpWins"] == first["state"]["pvpWins"] + (1 if outcome == "PlayerVictory" else 0)
                   and result_a["state"]["reputation"] == first["state"]["reputation"] - (1 if outcome == "OpponentVictory" else 0))
        published_a = json.loads(auth.sql("SELECT json_build_object('id',snapshot_id,'state',state)::text FROM pvp_snapshots WHERE match_id='" + ids[0] + "'"))
        auth.check("published_snapshot_is_actual_prebattle_state", json.loads(published_a["state"]) == original)
        auth.check("start_retry_after_completion_same_receipt", submit(0, start)[1] == pending)
        finish = command(result_a, "finish_pvp", battleId=battle_a)
        outcomes = parallel(0, finish)
        auth.check("concurrent_finish_one_round_transition", all(item[0] == 200 for item in outcomes) and outcomes[0][1] == outcomes[1][1])
        after_a = outcomes[0][1]
        cursors = saved(0)
        auth.check("new_round_income_cursor_once", after_a["state"]["round"] == 2 and after_a["state"]["turn"] == 1
                   and after_a["state"]["phase"] == "EncounterChoice" and cursors["incomeSettledThroughRound"] == 2
                   and cursors["experienceSettledThroughTurn"] == 9)
        a_hash = digest(0)
        second = normal_to_pvp(1)
        code, pending_b, _ = submit(1, command(second, "start_pvp", encounterKey="encounter.pvp"))
        auth.check("second_account_matches_real_first_account", code == 202 and not pending_b["state"]["pvp"]["isTestOpponent"]
                   and pending_b["state"]["pvp"]["opponentSnapshotId"] == published_a["id"])
        battle_b = pending_b["state"]["pvp"]["id"]
        release(battle_b)
        completed_b = wait_battle(1, battle_b)
        auth.check("real_snapshot_battle_completed", completed_b["result"]["state"]["phase"] == "PvpResult")
        auth.check("challenged_player_state_unchanged", digest(0) == a_hash)
        auth.check("source_snapshot_not_overwritten_by_battle", auth.sql("SELECT state FROM pvp_snapshots WHERE snapshot_id='" + published_a["id"] + "'") == published_a["state"])

        # c账号完整冻结输入为独立实例ID的明确夹具；真实旧执行器在新令牌下必须放弃提交。
        current_c = fixture(2, original)
        code, pending_c, _ = submit(2, command(current_c, "start_pvp", encounterKey="encounter.pvp"))
        auth.check("lease_fixture_queued", code == 202)
        battle_c = pending_c["state"]["pvp"]["id"]
        c_frozen = digest(2)
        c_input_hash = auth.sql("SELECT input_sha256 FROM pvp_battles WHERE battle_id='" + battle_c + "'")
        parent = auth.run(["sudo", "-n", "systemctl", "show", "project-star-api", "-p", "MainPID", "--value"])
        stopped_worker = next(pid for pid in auth.run(["pgrep", "-P", parent]).splitlines()
            if pathlib.Path(auth.run(["sudo", "-n", "readlink", "/proc/" + pid + "/exe"])).name == "project-star-rules.x86_64")
        auth.run(["sudo", "-n", "kill", "-STOP", stopped_worker])
        release(battle_c)
        for _ in range(40):
            if auth.request("/v1/battles/" + battle_c, token=tokens[2])[1]["status"] == "running":
                break
            time.sleep(0.1)
        auth.check("old_executor_running_on_paused_rules", auth.request("/v1/battles/" + battle_c, token=tokens[2])[1]["status"] == "running")
        new_token = str(uuid.uuid4())
        auth.sql("UPDATE pvp_battles SET lease_token='" + new_token + "',lease_until=now()+interval '60 seconds' WHERE battle_id='" + battle_c + "'")
        auth.run(["sudo", "-n", "kill", "-CONT", stopped_worker])
        stopped_worker = None
        auth.check("rules_resume_ready", auth.request("/health/ready")[0] == 200)
        time.sleep(1)
        auth.check("stale_executor_cannot_commit", digest(2) == c_frozen and auth.request("/v1/battles/" + battle_c, token=tokens[2])[1]["status"] == "running"
                   and auth.sql("SELECT count(*) FROM pvp_snapshots WHERE match_id='" + ids[2] + "'") == "0")
        auth.sql("UPDATE pvp_battles SET lease_until=now()-interval '1 second' WHERE battle_id='" + battle_c + "'")
        auth.restart()
        completed_c = wait_battle(2, battle_c)
        auth.check("expired_lease_recovers_after_restart", completed_c["attempts"] == 2
                   and completed_c["result"]["version"] == pending_c["version"] + 1
                   and auth.sql("SELECT input_sha256 FROM pvp_battles WHERE battle_id='" + battle_c + "'") == c_input_hash)
        auth.check("recovered_task_publishes_once", auth.sql("SELECT count(*) FROM pvp_snapshots WHERE match_id='" + ids[2] + "'") == "1")

        # d账号注入快照唯一键冲突，让SQL在写候选对局后失败；必须整体回滚而非重复结算。
        current_d = fixture(3, original)
        code, pending_d, _ = submit(3, command(current_d, "start_pvp", encounterKey="encounter.pvp"))
        auth.check("rollback_fixture_queued", code == 202)
        battle_d = pending_d["state"]["pvp"]["id"]
        d_frozen = digest(3)
        d_input = auth.sql("SELECT input_sha256 FROM pvp_battles WHERE battle_id='" + battle_d + "'")
        collision_id = auth.sql("SELECT published_snapshot_id FROM pvp_battles WHERE battle_id='" + battle_d + "'")
        auth.sql("INSERT INTO pvp_snapshots(snapshot_id,player_id,match_id,round,rules_version,state) SELECT published_snapshot_id,player_id,match_id,round,rules_version,player_snapshot FROM pvp_battles WHERE battle_id='" + battle_d + "'")
        fake_snapshots.append(collision_id)
        release(battle_d)
        failed = wait_battle(3, battle_d, "failed")
        auth.check("technical_failure_has_three_bounded_attempts", failed["attempts"] == 3 and failed["result"] is None)
        auth.check("failed_settlement_rolls_back_entire_match", digest(3) == d_frozen)
        auth.sql("DELETE FROM pvp_snapshots WHERE snapshot_id='" + collision_id + "'")
        fake_snapshots.remove(collision_id)
        retry = command(pending_d, "retry_pvp", battleId=battle_d)
        outcomes = parallel(3, retry)
        auth.check("explicit_retry_same_receipt_and_input", all(item[0] == 202 for item in outcomes) and outcomes[0][1] == outcomes[1][1]
                   and outcomes[0][1]["version"] == pending_d["version"])
        completed_d = wait_battle(3, battle_d)
        auth.check("repaired_task_commits_once", completed_d["result"]["version"] == pending_d["version"] + 1
                   and auth.sql("SELECT input_sha256 FROM pvp_battles WHERE battle_id='" + battle_d + "'") == d_input
                   and auth.sql("SELECT count(*) FROM pvp_snapshots WHERE match_id='" + ids[3] + "'") == "1")

        # e账号9胜及强攻击为明确数值夹具，验证终局归档释放单活动局限制。
        current_e = fixture(4, original, wins=9, boost=True)
        code, pending_e, _ = submit(4, command(current_e, "start_pvp", encounterKey="encounter.pvp"))
        auth.check("terminal_fixture_queued", code == 202)
        battle_e = pending_e["state"]["pvp"]["id"]
        release(battle_e)
        completed_e = wait_battle(4, battle_e)
        terminal = completed_e["result"]
        auth.check("tenth_win_archives_completed_round", terminal["state"]["status"] == "Won" and terminal["state"]["pvpWins"] == 10
                   and terminal["state"]["matchSummary"]["completedRounds"] == 1)
        old_e = ids[4]
        auth.check("ended_match_readable_by_owner", auth.request("/v1/matches/" + old_e, token=tokens[4])[1] == terminal)
        auth.check("ended_match_not_readable_by_other_player", auth.request("/v1/matches/" + old_e, token=tokens[1])[0] == 404)
        auth.check("ended_match_removed_from_current", auth.request("/v1/matches/current", token=tokens[4])[0] == 404)
        code, replacement, _ = auth.request("/v1/matches", {"requestId": str(uuid.uuid4()), "heroKey": "hero.paladin"}, tokens[4])
        auth.check("new_match_after_terminal_allowed", code == 201 and replacement["matchId"] != old_e)
        ids[4] = replacement["matchId"]
        paths[4] = "/v1/matches/" + ids[4] + "/commands"
        current_e = fixture(4, original)
        code, pending_e, _ = submit(4, command(current_e, "start_pvp", encounterKey="encounter.pvp"))
        auth.check("next_match_excludes_own_published_snapshot", code == 202 and auth.sql("SELECT player_id FROM pvp_snapshots WHERE snapshot_id='"
                   + pending_e["state"]["pvp"]["opponentSnapshotId"] + "'") != players[4])
        release(pending_e["state"]["pvp"]["id"])
        wait_battle(4, pending_e["state"]["pvp"]["id"])
        auth.check("old_terminal_battle_result_preserved", auth.request("/v1/battles/" + battle_e, token=tokens[4])[1] == completed_e)
        auth.check("other_challenger_does_not_modify_first_account", digest(0) == a_hash)

        auth.sql("DROP TRIGGER IF EXISTS " + trigger_name + " ON pvp_battles; DROP FUNCTION IF EXISTS " + trigger_name + "()")
        backup = "/var/lib/project-star/backups/" + prefix + ".dump"
        job_query = "SELECT md5(input_state || ':' || COALESCE(result::text,'')) FROM pvp_battles WHERE battle_id='" + battle_b + "'"
        before = auth.sql(job_query)
        auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
        auth.run(["sudo", "-n", "chmod", "0600", backup])
        restore_db = "project_star_restore_" + uuid.uuid4().hex[:12]
        auth.run(["sudo", "-n", "-u", "postgres", "createdb", "--owner=project-star", restore_db])
        try:
            auth.run(["sudo", "-n", "-u", "project-star", "pg_restore", "--no-owner", "--exit-on-error", "-d", restore_db, backup])
            auth.check("backup_restores_frozen_inputs_and_result", auth.sql(job_query, restore_db) == before)
            auth.check("backup_restores_published_snapshot", auth.sql("SELECT state FROM pvp_snapshots WHERE snapshot_id='" + published_a["id"] + "'", restore_db) == published_a["state"])
        finally:
            auth.run(["sudo", "-n", "-u", "postgres", "dropdb", restore_db])
        print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED,
            "testAccountPrefix": prefix, "matchA": ids[0], "matchB": ids[1], "battleA": battle_a, "battleB": battle_b,
            "leaseBattle": battle_c, "rollbackBattle": battle_d, "terminalBattle": battle_e, "backup": backup}, ensure_ascii=False))
    finally:
        if stopped_worker is not None:
            auth.run(["sudo", "-n", "kill", "-CONT", stopped_worker])
        for snapshot_id in fake_snapshots:
            auth.sql("DELETE FROM pvp_snapshots WHERE snapshot_id='" + snapshot_id + "'")
        auth.sql("DROP TRIGGER IF EXISTS " + trigger_name + " ON pvp_battles; DROP FUNCTION IF EXISTS " + trigger_name + "()")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
