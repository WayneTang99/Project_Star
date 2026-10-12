using System;
using Godot;
using System.Collections.Generic;
using Project_Star.Application.Board;
using Project_Star.Domain.Definitions;
using Project_Star.Application.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 在每轮开始边界幂等结算英雄收入（应用层）。
public sealed class RoundIncomeService
{
    private static readonly StringName MissingHero = new("income.missing_hero");
    private readonly IReadOnlyDictionary<StringName, CardSetDefinition>? _sets;
    private readonly IDefinitionCatalog? _catalog;

    public RoundIncomeService(IReadOnlyDictionary<StringName, CardSetDefinition>? sets = null,
        IDefinitionCatalog? catalog = null)
    {
        _sets = sets;
        _catalog = catalog;
    }

    // 为尚未结算的当前轮发放一次英雄收入。
    public Result<int> SettleCurrentRound(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Player.Hero is null)
            return Result<int>.Fail(new Failure(MissingHero, "A hero is required to settle round income."));
        if (session.Progress.IncomeSettledThroughRound >= session.Progress.Round)
            return Result<int>.Success(0);
        var income = session.Player.Income;
        if (income < 0) throw new InvalidOperationException("Income cannot be negative.");
        session.Player.AddWealth(income);
        if (_sets is not null)
        {
            MatchAbilityService.ApplyRoundSetCooldowns(session, _sets);
            var experience = MatchAbilityService.RoundExperience(session, _sets);
            if (experience > 0) session.Player.AddExperience(experience);
            new CardSetBonusService(_sets).Recalculate(session);
        }
        session.Progress.IncomeSettledThroughRound = session.Progress.Round;
        if (_catalog is not null)
            MatchAbilityService.ApplyRoundCards(session, _catalog,
                new BoardService(new BoardPlacementSolver(), _catalog.Sets));
        session.Events.Add(new IncomeGrantedEvent(session.Progress.Round, income, session.Player.Wealth));
        return Result<int>.Success(income);
    }
}
