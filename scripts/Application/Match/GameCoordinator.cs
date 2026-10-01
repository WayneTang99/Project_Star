using System;
using System.Collections.Generic;
using Project_Star.Application.Combat;
using Project_Star.Application.Common;
using Project_Star.Application.Encounters;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 应用层协调对局创建、遭遇推进和一次战斗结算。
public sealed class GameCoordinator
{
    private readonly CreateMatchService _matches;
    private readonly EncounterScheduler _encounters;
    private readonly StartBattleService _battles;
    private readonly MatchResultService _results;

    public GameCoordinator(
        CreateMatchService matches,
        EncounterScheduler encounters,
        StartBattleService battles,
        MatchResultService results)
    {
        _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
        _battles = battles ?? throw new ArgumentNullException(nameof(battles));
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    public MatchSession CreateMatch(ulong seed, int startingWealth, HeroDefinition hero) =>
        _matches.Create(seed, startingWealth, hero);

    public MatchSnapshot GetSnapshot(MatchSession session) => MatchSnapshot.From(session);

    public Result<IReadOnlyList<EncounterChoice>> GenerateEncounterChoices(MatchSession session) =>
        _encounters.Generate(session);

    public Result<EncounterSelectionResult> SelectEncounter(MatchSession session, Godot.StringName encounterKey) =>
        _encounters.Select(session, encounterKey);

    // 返回同一次结算结果，沿用不需要播放数据的调用入口。
    public Result<BattleResult> ResolveBattle(
        MatchSession player,
        MatchSession opponent,
        MatchBattleKind kind,
        int battleRound)
    {
        var resolution = ResolveBattleForPlayback(player, opponent, kind, battleRound);
        return resolution.IsFailure ? Result<BattleResult>.Fail(resolution.Failure!)
            : Result<BattleResult>.Success(resolution.Value!.Result);
    }

    // 一次完成结算并返回战前、战斗记录和结算后快照；回放不再执行本用例。
    public Result<BattleResolution> ResolveBattleForPlayback(
        MatchSession player, MatchSession opponent, MatchBattleKind kind, int battleRound)
    {
        var playerBefore = MatchSnapshot.From(player);
        var opponentBefore = MatchSnapshot.From(opponent);
        var battle = _battles.StartBattle(player, opponent, player.Random.State);
        if (battle.IsFailure) return Result<BattleResolution>.Fail(battle.Failure!);

        var settlement = _results.Apply(player, battle.Value!, kind, battleRound, opponent);
        if (settlement.IsFailure) return Result<BattleResolution>.Fail(settlement.Failure!);
        return Result<BattleResolution>.Success(new BattleResolution(playerBefore, opponentBefore,
            battle.Value!, MatchSnapshot.From(player)));
    }

    public Result Surrender(MatchSession session, MatchBattleKind kind) =>
        _results.Surrender(session, kind);
}
