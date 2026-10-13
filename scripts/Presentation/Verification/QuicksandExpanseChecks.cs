using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Random;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 流沙探险的概率边界、等级、随机复现与奖励放置验证。
internal static class QuicksandExpanseChecks
{
    internal static bool DefinitionsAndScheduling()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var desert = (QuicksandExpanseEncounterDefinition)registry.Encounters[new StringName("encounter.quicksand_expanse")];
        if (registry.Encounters[new StringName("encounter.dusk_song_jungle")] is not ExplorationEncounterDefinition
            || desert.Level != 2 || desert.MinimumRound != 1 || desert.MaximumRound != 99 || desert.BaseWeight != 1
            || desert.Options.Count != 2 || desert.OptionSlots.Count != 2
            || desert.Options[0].DisplayName != "淘金" || desert.Options[1].DisplayName != "穿越沙暴"
            || desert.Options[0].Effects.Single() is not GainRandomWealthEncounterOptionEffectDefinition { MinimumAmount: 2, MaximumAmount: 8 }) return false;
        var outcomes = (WeightedEncounterOptionEffectDefinition)desert.Options[1].Effects.Single();
        if (!outcomes.Outcomes.Select(value => value.Weight).SequenceEqual(new[] { 60, 30, 10 })
            || outcomes.Outcomes[0].Effect is not GrantRandomFactionCardEncounterOptionEffectDefinition { Size: CardSize.Medium, UseEncounterLevel: true }
            || outcomes.Outcomes[1].Effect is not EnterRandomMonsterEncounterOptionEffectDefinition
            || outcomes.Outcomes[2].Effect is not GrantCardEncounterOptionEffectDefinition { UseEncounterLevel: true } bag
            || bag.CardKey != new StringName("card.jewelry_bag")) return false;
        var description = PlaytestText.FormatOption(desert.Options[1], 1, 4, registry.Cards);
        if (!description.Contains("60%") || !description.Contains("30%") || !description.Contains("10%")
            || !description.Contains("4 级本阵营中型") || !description.Contains("4 级珠宝袋")
            || !PlaytestText.FormatOption(desert.Options[0], 1, 2).Contains("2～8")) return false;
        if (GD.Load<Texture2D>(desert.Attributes.Identity.Illustration.ToString()) is null) return false;
        var schedulerRegistry = DefinitionRegistry.Create([desert, new SmallShopEncounterDefinition(), new JewelryBagCardDefinition()]);
        foreach (var round in new[] { 1, 2, 3, 99 })
        {
            var session = new MatchSession(1); session.Progress.Round = round;
            var choices = new EncounterScheduler(schedulerRegistry).Generate(session);
            if (choices.IsFailure || choices.Value!.Any(value => value.Key == desert.Attributes.Identity.Key) != (round >= 3)) return false;
        }
        foreach (var invalid in new EncounterOptionEffectDefinition[]
        {
            new GainRandomWealthEncounterOptionEffectDefinition(0, 8),
            new GainRandomWealthEncounterOptionEffectDefinition(8, 2),
            new GainRandomWealthEncounterOptionEffectDefinition(2, int.MaxValue),
            new GrantCardEncounterOptionEffectDefinition(new StringName("card.missing"), 2),
        })
        {
            try { DefinitionRegistry.Create([new EffectEncounter(invalid)]); return false; }
            catch (DefinitionValidationException) { }
        }
        return true;
    }

    internal static bool OutcomesAndLevels()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var desert = new QuicksandExpanseEncounterDefinition();
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var matches = new CreateMatchService(factory);
        MonsterDefinition[] monsters = [new TestMonster(2), new TestMonster(3), new TestMonster(4)];
        var resolver = new ResolveEncounterOptionService(factory, board, registry.Cards.Values, monsters);
        foreach (var amount in Enumerable.Range(2, 7))
        {
            var first = Resolve(SeedForRoll(amount - 2, 7), 0, 2, new StringName("hero.paladin"));
            var second = Resolve(SeedForRoll(amount - 2, 7), 0, 2, new StringName("hero.paladin"));
            if (first.Result.WealthGained != amount || first.Session.Player.Wealth != 5 + amount
                || first.Result.WealthGained != second.Result.WealthGained || first.Session.Random.State != second.Session.Random.State) return false;
        }
        foreach (var heroKey in new[] { new StringName("hero.paladin"), new StringName("hero.mona") })
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var roll in new[] { 0, 59, 60, 89, 90, 99 })
        {
            var first = Resolve(SeedForRoll(roll, 100), 1, level, heroKey);
            var second = Resolve(SeedForRoll(roll, 100), 1, level, heroKey);
            if (first.Session.Random.State != second.Session.Random.State || first.Session.Progress.Turn != 1
                || first.Result.WealthGained != 0 || first.Session.Player.Wealth != 5) return false;
            if (roll is >= 60 and < 90)
            {
                if (first.Result.GrantedCard is not null || first.Result.MonsterEncounter is not { } monster
                    || monster.Level != level || monster != second.Result.MonsterEncounter) return false;
            }
            else
            {
                var card = first.Result.GrantedCard;
                if (card is null || card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != level
                    || card.Attributes.Identity.Key != second.Result.GrantedCard?.Attributes.Identity.Key
                    || first.Session.Board.Battlefield.Count != 1 || first.Result.MonsterEncounter is not null) return false;
                if (roll < 60 && (card.Attributes.Identity.Size != CardSize.Medium
                    || card.Attributes.Identity.FactionKey != registry.Heroes[heroKey].Attributes.Identity.FactionKey)) return false;
                if (roll >= 90 && card.Attributes.Identity.Key != new StringName("card.jewelry_bag")) return false;
            }
        }
        var emptyResolver = new ResolveEncounterOptionService(factory, board, registry.Cards.Values);
        var formalResolver = new ResolveEncounterOptionService(factory, board, registry.Cards.Values, registry.Monsters.Values);
        foreach (var exploration in new ExplorationEncounterDefinition[] { desert, new DuskSongJungleEncounterDefinition() })
        foreach (var visitLevel in new[] { 2, 3, 4 })
        {
            var formalSession = matches.Create(1, 0, registry.Heroes[new StringName("hero.paladin")]);
            formalSession.Progress.Round = 3;
            var formalOptions = formalResolver.CreateOptionSet(formalSession, exploration, visitLevel);
            formalSession.Random.State = SeedForRoll(60, 100);
            var formalResult = formalResolver.Resolve(formalSession, formalOptions, exploration.Options[1].Key);
            if (formalResult.IsFailure) return false;
            if (visitLevel == 2)
            {
                if (formalResult.Value!.MonsterEncounter is not { Level: 2 } encounter
                    || encounter.Key != new StringName("monster.boar") || formalResult.Value.MonsterEncounterSkipped) return false;
            }
            else if (!formalResult.Value!.MonsterEncounterSkipped || formalResult.Value.MonsterEncounter is not null) return false;
        }
        var emptySession = matches.Create(1, 0, registry.Heroes[new StringName("hero.paladin")]);
        var emptyOptions = emptyResolver.CreateOptionSet(emptySession, desert, 2);
        emptySession.Random.State = SeedForRoll(60, 100);
        if (emptyResolver.Resolve(emptySession, emptyOptions, desert.Options[1].Key).Value is not { MonsterEncounterSkipped: true }) return false;
        foreach (var roll in new[] { 0, 90 })
        {
            var full = matches.Create(1, 0, registry.Heroes[new StringName("hero.paladin")]);
            var economy = new CardEconomyService(factory);
            for (var index = 0; index < 20; index++)
            {
                var blocker = economy.AcquireCard(full, new BeastHideCardDefinition(), 4, CardAcquisitionSource.Reward).Value!.Card;
                if (board.PlaceCard(full, blocker.Id, index < 10 ? BoardZone.Battlefield : BoardZone.Bench, index % 10).IsFailure) return false;
            }
            var options = resolver.CreateOptionSet(full, desert, 2); full.Random.State = SeedForRoll(roll, 100);
            if (resolver.Resolve(full, options, desert.Options[1].Key).Value is not { CardRewardSkipped: true, GrantedCard: null }
                || full.Player.Inventory.Cards.Count != 20) return false;
        }
        return true;

        (MatchSession Session, EncounterOptionResult Result) Resolve(ulong seed, int optionIndex, int level, StringName heroKey)
        {
            var session = matches.Create(1, 0, registry.Heroes[heroKey]); session.Progress.Round = 3;
            var options = resolver.CreateOptionSet(session, desert, level); session.Random.State = seed;
            var result = resolver.Resolve(session, options, desert.Options[optionIndex].Key);
            if (result.IsFailure || !options.IsResolved) throw new InvalidOperationException("流沙奖励结算失败。");
            var state = session.Random.State;
            if (resolver.Resolve(session, options, desert.Options[optionIndex].Key).IsSuccess || session.Random.State != state)
                throw new InvalidOperationException("探险选项重复结算。");
            return (session, result.Value!);
        }
    }

    private static ulong SeedForRoll(int roll, int maximum)
    {
        for (ulong seed = 0; seed < 10000; seed++) if (new SeededRandom(seed).NextInt(0, maximum) == roll) return seed;
        throw new InvalidOperationException("未找到流沙概率边界seed。");
    }

    private sealed class TestMonster : MonsterDefinition
    {
        internal TestMonster(int level) : base(new StringName("monster.verification.quicksand." + level), "沙暴验证怪物",
            Array.Empty<MonsterCardEntry>(), level: level, minimumRound: 3, maximumRound: 3) { }
    }

    private sealed class EffectEncounter : ChoiceEncounterDefinition
    {
        internal EffectEncounter(EncounterOptionEffectDefinition effect) : base(new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.verification.quicksand"), "流沙验证")), 1, 99,
            [new EncounterOptionDefinition(new StringName("encounter.verification.quicksand.choose"), "选择", [effect])]) { }
    }
}
