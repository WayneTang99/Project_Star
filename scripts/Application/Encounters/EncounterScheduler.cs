using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Encounters;

/// <summary>Generates deterministic encounter choices and advances selected turns.</summary>
public sealed class EncounterScheduler
{
    private static readonly StringName MissingShop = new("encounter.missing_shop");
    private static readonly StringName MissingSpecial = new("encounter.missing_special");
    private static readonly StringName InvalidChoice = new("encounter.invalid_choice");

    private readonly DefinitionRegistry _registry;
    private readonly bool _allowIncompleteMonsterChoices;

    public EncounterScheduler(DefinitionRegistry registry, bool allowIncompleteMonsterChoices = false)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _allowIncompleteMonsterChoices = allowIncompleteMonsterChoices;
    }

    public Result<IReadOnlyList<EncounterChoice>> Generate(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Player.Hero is not null)
        {
            var income = new RoundIncomeService().SettleCurrentRound(session);
            if (income.IsFailure) return Fail(income.Failure!.Code, income.Failure.Message);
        }
        var random = new SeededRandom(session.Random.State);
        var available = _registry.Encounters.Values
            .Where(value => value.MinimumRound <= session.Progress.Round
                && session.Progress.Round <= value.MaximumRound)
            .OrderBy(value => value.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .ToList();
        var allDefinitions = _registry.Encounters.Values
            .Concat(_registry.Monsters.Values.Select(monster => new RegisteredMonsterEncounterDefinition(monster)))
            .OrderBy(value => value.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .ToList();
        List<EncounterDefinition> selected;

        if (session.Progress.Turn == 4)
        {
            selected = SelectMany(allDefinitions.Where(value => value.Kind == EncounterKind.Monster).ToList(), 3, session, random);
            if (selected.Count < 3 && (!_allowIncompleteMonsterChoices || selected.Count == 0))
                return Fail(MissingSpecial, "Turn four requires three different monster encounters.");
        }
        else if (session.Progress.Turn == 8)
        {
            var pvp = allDefinitions.FirstOrDefault(value => value.Kind == EncounterKind.Pvp);
            if (pvp is null) return Fail(MissingSpecial, "Turn eight requires a PvP encounter.");
            selected = [pvp];
        }
        else
        {
            available = available
                .Where(value => value.Kind is EncounterKind.Shop or EncounterKind.Other)
                .ToList();
            var shops = available.Where(value => value.Kind == EncounterKind.Shop).ToList();
            if (shops.Count == 0) return Fail(MissingShop, "A normal turn requires at least one shop encounter.");
            var shop = SelectOne(shops, session, random);
            available.Remove(shop);
            selected = [shop];
            selected.AddRange(SelectMany(available, 2, session, random));
        }

        var choices = selected.Select(ToChoice).ToList().AsReadOnly();
        session.EncounterSchedule.SetChoices(choices);
        session.Random.State = random.State;
        session.Events.Add(new EncounterChoicesGeneratedEvent(session.Progress.Round, session.Progress.Turn, choices.Count));
        return Result<IReadOnlyList<EncounterChoice>>.Success(choices);
    }

    public Result<EncounterSelectionResult> Select(MatchSession session, StringName encounterKey)
    {
        ArgumentNullException.ThrowIfNull(session);
        EncounterChoice? selected = null;
        foreach (var choice in session.EncounterSchedule.CurrentChoices)
            if (choice.Key == encounterKey) { selected = choice; break; }
        if (selected is null)
            return Result<EncounterSelectionResult>.Fail(new Failure(InvalidChoice, "The encounter is not in the current choices."));

        session.Events.Add(new EncounterSelectedEvent(selected.Key, selected.Kind));
        session.EncounterSchedule.ClearChoices();
        if (session.Progress.Turn == 8)
        {
            session.Progress.Round++;
            session.Progress.Turn = 1;
        }
        else session.Progress.Turn++;
        session.Events.Add(new TurnAdvancedEvent(session.Progress.Round, session.Progress.Turn));
        return Result<EncounterSelectionResult>.Success(
            new EncounterSelectionResult(selected, selected.Kind is EncounterKind.Monster or EncounterKind.Pvp));
    }

    private static List<EncounterDefinition> SelectMany(
        List<EncounterDefinition> pool,
        int count,
        MatchSession session,
        SeededRandom random)
    {
        var selected = new List<EncounterDefinition>();
        while (pool.Count > 0 && selected.Count < count)
        {
            var item = SelectOne(pool, session, random);
            selected.Add(item);
            pool.Remove(item);
        }
        return selected;
    }

    private static EncounterDefinition SelectOne(
        IReadOnlyList<EncounterDefinition> pool,
        MatchSession session,
        SeededRandom random)
    {
        var total = 0;
        foreach (var item in pool)
            total = checked(total + item.BaseWeight * (session.EncounterSchedule.SeenKeys.Contains(item.Attributes.Identity.Key) ? 1 : 2));
        var roll = random.NextInt(0, total);
        foreach (var item in pool)
        {
            roll -= item.BaseWeight * (session.EncounterSchedule.SeenKeys.Contains(item.Attributes.Identity.Key) ? 1 : 2);
            if (roll < 0) return item;
        }
        throw new InvalidOperationException("Weighted selection did not produce an encounter.");
    }

    private static EncounterChoice ToChoice(EncounterDefinition definition) =>
        new(definition.Attributes.Identity.Key, definition.Attributes.Identity.DisplayName, definition.Kind);

    private sealed class RegisteredMonsterEncounterDefinition : EncounterDefinition
    {
        public RegisteredMonsterEncounterDefinition(MonsterDefinition monster)
            : base(
                new EntityAttributes<EncounterIdentityAttributes>(
                    new EncounterIdentityAttributes(
                        monster.Attributes.Identity.Key,
                        monster.Attributes.Identity.DisplayName)),
                1,
                99,
                EncounterKind.Monster)
        {
        }
    }

    private static Result<IReadOnlyList<EncounterChoice>> Fail(StringName code, string message) =>
        Result<IReadOnlyList<EncounterChoice>>.Fail(new Failure(code, message));
}
