using System;
using System.Collections.Generic;
using Project_Star.Application.Combat;
using Project_Star.Application.Common;
using Project_Star.Application.Encounters;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

/// <summary>Coordinates match creation, encounter progression, battles, and settlement.</summary>
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

    public Result<BattleResult> ResolveBattle(
        MatchSession player,
        MatchSession opponent,
        MatchBattleKind kind,
        int battleRound)
    {
        var battle = _battles.StartBattle(player, opponent, player.Random.State);
        if (battle.IsFailure) return Result<BattleResult>.Fail(battle.Failure!);

        var settlement = _results.Apply(player, battle.Value!, kind, battleRound, opponent);
        if (settlement.IsFailure) return Result<BattleResult>.Fail(settlement.Failure!);
        return Result<BattleResult>.Success(battle.Value!);
    }

    public Result Surrender(MatchSession session, MatchBattleKind kind) =>
        _results.Surrender(session, kind);
}
