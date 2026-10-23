#!/usr/bin/env python3
"""接续已完成PvP专项的两个qa账号，正常跨轮交叉消费快照；仅重置指定qa前缀的测试密码。"""
import json
import re
import secrets
import sys
import time
import uuid
import VerifyAccountHost as auth


def main(prefix):
    if not re.fullmatch(r"qa_pvp_[0-9a-f]{9}", prefix):
        raise AssertionError("qa_prefix_required")
    tokens, players, states = [], [], []
    for suffix in "ab":
        password = secrets.token_urlsafe(24)
        auth.admin("reset-password", prefix + suffix, password=password + "\n")
        code, session, _ = auth.request("/v1/auth/login", {"username": prefix + suffix, "password": password})
        auth.check("qa_relogin_" + suffix, code == 200)
        tokens.append(session["accessToken"])
        players.append(session["playerId"])
        code, state, _ = auth.request("/v1/matches/current", token=tokens[-1])
        auth.check("resume_second_round_" + suffix, code == 200 and state["state"]["round"] == 2)
        states.append(state)

    def send(index, state, kind, **fields):
        return auth.request("/v1/matches/" + state["matchId"] + "/commands", {
            "requestId": str(uuid.uuid4()), "expectedMatchVersion": state["version"], "type": kind, **fields}, tokens[index])

    def claim_all(index, state, battle_id):
        for _ in range(40):
            if not state["state"]["pendingRewards"]:
                return state
            code, next_state, _ = send(index, state, "claim_reward", battleId=battle_id, rewardIndex=0)
            if code == 422 and state["state"]["cards"]:
                card = min(state["state"]["cards"], key=lambda item: item["value"])
                code, next_state, _ = send(index, state, "sell_card", cardId=card["id"])
            if code != 200:
                raise AssertionError("cross_round_claim_or_space_failed")
            state = next_state
        raise AssertionError("cross_round_reward_bound")

    def advance(index, current):
        if current["state"]["phase"] == "PvpResult":
            battle_id = current["state"]["pvp"]["id"]
            current = claim_all(index, current, battle_id)
            code, current, _ = send(index, current, "finish_pvp", battleId=battle_id)
            if code != 200:
                raise AssertionError("cross_round_finish_prior")
        while current["state"]["turn"] < 8:
            choices = current["state"]["choices"]
            choice = next((item for item in choices if item["isChoiceEvent"]),
                          next((item for item in choices if item["isCardShop"]), choices[0]))
            if choice["kind"] == "Monster":
                choice = next((item for item in choices if item["key"] == "monster.fire_slime"), choice)
                code, current, _ = send(index, current, "select_monster", encounterKey=choice["key"])
                if code != 200:
                    raise AssertionError("cross_monster_select")
                battle_id = current["state"]["battle"]["id"]
                code, current, _ = send(index, current, "resolve_monster", battleId=battle_id)
                if code != 200:
                    raise AssertionError("cross_monster_resolve")
                current = claim_all(index, current, battle_id)
                code, current, _ = send(index, current, "finish_monster", battleId=battle_id)
            elif choice["isChoiceEvent"]:
                code, current, _ = send(index, current, "select_event", encounterKey=choice["key"])
                if code != 200:
                    raise AssertionError("cross_event_select")
                visit = current["state"]["eventVisit"]
                option = next(item for item in visit["options"] if not item["key"].endswith(".trade"))
                code, current, _ = send(index, current, "resolve_event", visitId=visit["id"], optionKey=option["key"])
                if code != 200:
                    raise AssertionError("cross_event_resolve")
                code, current, _ = send(index, current, "leave_event", visitId=visit["id"])
            else:
                code, current, _ = send(index, current, "select_shop", encounterKey=choice["key"])
                if code != 200:
                    raise AssertionError("cross_shop_select")
                code, current, _ = send(index, current, "leave_shop")
            if code != 200:
                raise AssertionError("cross_round_advance")
        auth.check("normal_second_round_turn_eight_" + str(index), current["state"]["round"] == 2
                   and current["state"]["choices"][0]["kind"] == "Pvp")
        return current

    battles = []
    # b先发布第2轮快照，a再消费；第1轮已验证b消费a，形成实际双向关系。
    for index in (1, 0):
        current = advance(index, states[index])
        code, pending, _ = send(index, current, "start_pvp", encounterKey=current["state"]["choices"][0]["key"])
        auth.check("second_round_start_" + str(index), code == 202)
        battle_id = pending["state"]["pvp"]["id"]
        source = pending["state"]["pvp"]["opponentSnapshotId"]
        other_id = states[1 - index]["matchId"]
        other_hash = auth.sql("SELECT md5(state::text)||':'||version FROM matches WHERE match_id='" + other_id + "'")
        if index == 0:
            auth.check("a_consumes_real_b_second_round_snapshot", source is not None and not pending["state"]["pvp"]["isTestOpponent"]
                       and auth.sql("SELECT player_id FROM pvp_snapshots WHERE snapshot_id='" + source + "'") == players[1])
        for _ in range(120):
            code, result, _ = auth.request("/v1/battles/" + battle_id, token=tokens[index])
            if code == 200 and result["status"] == "completed":
                break
            if code != 200 or result["status"] == "failed":
                raise AssertionError("cross_round_task_failed")
            time.sleep(0.25)
        else:
            raise AssertionError("cross_round_task_timeout")
        auth.check("second_round_settlement_" + str(index), result["result"]["state"]["pvp"]["round"] == 2)
        auth.check("second_round_defender_unchanged_" + str(index),
            auth.sql("SELECT md5(state::text)||':'||version FROM matches WHERE match_id='" + other_id + "'") == other_hash)
        current = claim_all(index, result["result"], battle_id)
        code, states[index], _ = send(index, current, "finish_pvp", battleId=battle_id)
        auth.check("continue_third_round_" + str(index), code == 200 and states[index]["state"]["round"] == 3 and states[index]["state"]["turn"] == 1)
        battles.append(battle_id)
    auth.check("both_normal_accounts_publish_and_consume_real_snapshots", all(auth.sql("SELECT count(*) FROM pvp_snapshots WHERE player_id='" + player + "'") == "2" for player in players)
        and all(int(auth.sql("SELECT count(*) FROM pvp_battles b JOIN pvp_snapshots s ON s.snapshot_id=b.opponent_snapshot_id WHERE b.player_id='"
                            + player + "' AND s.player_id='" + players[1 - index] + "' AND b.status='completed'")) >= 1 for index, player in enumerate(players)))
    backup = "/var/lib/project-star/backups/" + prefix + "_cross.dump"
    auth.run(["sudo", "-n", "-u", "project-star", "pg_dump", "-Fc", "-f", backup, "project_star"])
    auth.run(["sudo", "-n", "chmod", "0600", backup])
    print(json.dumps({"date": "2026-10-23", "checks": len(auth.PASSED), "passed": auth.PASSED,
        "testAccountPrefix": prefix, "battles": battles, "backup": backup}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main(sys.argv[1])
    except Exception as error:
        print("FAIL " + (str(error) if isinstance(error, AssertionError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
