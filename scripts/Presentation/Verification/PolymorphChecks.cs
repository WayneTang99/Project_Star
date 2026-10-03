using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 咩咩羊与转变的分级、队列、状态清除及回放隔离验证（表现层验证模块）。
internal static class PolymorphChecks
{
    internal static bool LevelsAndHaste()
    {
        var sheep = new BaaSheepCardDefinition();
        var wand = new PolymorphWandCardDefinition();
        var catalog = DefinitionRegistry.Scan(typeof(BaaSheepCardDefinition).Assembly);
        if (!catalog.Cards.ContainsKey(sheep.Attributes.Identity.Key)
            || !catalog.Cards.ContainsKey(wand.Attributes.Identity.Key)
            || sheep.Attributes.Identity.DisplayName != "咩咩羊" || sheep.InitialLevel != 1
            || sheep.Attributes.Identity.FactionKey != GameFactions.Neutral
            || sheep.Attributes.Identity.Size != CardSize.Small || !sheep.Tags.Contains(GameTags.Beast)
            || !sheep.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || wand.Attributes.Identity.DisplayName != "变羊魔棒" || wand.InitialLevel != 2
            || wand.Attributes.Identity.FactionKey != new StringName("mona")
            || wand.Attributes.Identity.Size != CardSize.Medium || !wand.Tags.Contains(GameTags.Equipment)
            || !wand.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || wand.SupportsLevel(1) || wand.SupportsLevel(5) || sheep.SupportsLevel(5)) return false;
        for (var level = 1; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(sheep, level);
            var secondActivation = level < 3 ? 100 - level * 10 : 75;
            var result = new CombatSimulator().Simulate(Battle([Card(instance.Id) with
                { Abilities = instance.Abilities, Level = level }], [], secondActivation));
            var activations = result.Events.OfType<AbilityActivatedEvent>().Select(item => item.Tick.Value);
            if (!activations.SequenceEqual(new long[] { 50, secondActivation })
                || result.Events.OfType<StatusChangedEvent>().Any(item => item.Amount != level * 10)
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 50) return false;
        }
        for (var level = 2; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(wand, level);
            var cooldown = (10 - level) * 10;
            var result = new CombatSimulator().Simulate(Battle([Card(instance.Id) with
                { Abilities = instance.Abilities, OccupiedSlots = 2 }], [Card(EntityId.New())], cooldown));
            if (instance.Abilities.Single().ManaCost != 0
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != cooldown
                || result.Events.OfType<CardTransformedEvent>().Single().Tick.Value != cooldown) return false;
        }
        try
        {
            DefinitionRegistry.Create([new MonaHeroDefinition(), wand]);
            return false;
        }
        catch (DefinitionValidationException) { return true; }
    }

    internal static bool TargetsAndReset()
    {
        var source = Card(EntityId.New()) with { Abilities = new PolymorphWandCardDefinition().GetLevel(2)!.Abilities };
        var first = Card(EntityId.New()); var second = Card(EntityId.New()) with { BoardStart = 1 };
        var medium = Card(EntityId.New()) with { OccupiedSlots = 2 };
        var large = Card(EntityId.New()) with { OccupiedSlots = 3 };
        var bench = Card(EntityId.New()) with { IsOnBench = true };
        var dead = Card(EntityId.New()) with { Abilities = [Ability("destroy", AbilityActivation.PassiveOnBattleStart, 0,
            new DestroyCardEffectDefinition(false))] };
        var selections = new HashSet<EntityId>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Battle([source], [first, second, medium, large, bench, dead], 80, seed);
            var result = new CombatSimulator().Simulate(setup);
            var selected = result.Events.OfType<CardTransformedEvent>().Single().TargetCardId;
            if (selected != first.EntityId && selected != second.EntityId
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            selections.Add(selected);
        }
        if (selections.Count != 2 || new CombatSimulator().Simulate(Battle([source], [medium, large, bench, dead], 80))
            .Events.OfType<CardTransformedEvent>().Any()) return false;
        for (var level = 1; level <= 4; level++)
        {
            var target = first with { Level = level, Abilities = [Ability("old", AbilityActivation.Active, 80,
                new DamageEffectDefinition(900))] };
            var result = new CombatSimulator().Simulate(Battle([source], [target], 130));
            var after = result.States.First(frame => frame.Cards.Any(card => card.TransformedIdentity is not null))
                .Cards.Single(card => card.Id == first.EntityId);
            if (after.Level != level || after.CooldownUnits.Single() != 100
                || after.TransformedIdentity!.DisplayName != "咩咩羊"
                || result.Events.OfType<DamageDealtEvent>().Any(item => item.SourceKind == DamageSourceKind.Card)
                || result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == first.EntityId).Tick.Value != 130
                || result.States[^1].Cards.Single(card => card.Id == first.EntityId).Haste != level * 10) return false;
        }
        var opening = Ability("states", AbilityActivation.PassiveOnBattleStart, 0,
            new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 200),
            new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 200),
            new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 200),
            new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true),
            new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true),
            new IncreaseSourceCooldownEffectDefinition(30));
        var dirty = first with { Level = 4, AttackDamage = 500, ArmorAmount = 400, Multicast = 3,
            CooldownMultiplier = .5m, Tags = new TagSet([GameTags.Human]), ElementKeys = [GameElements.Fire],
            Abilities = [opening, Ability("attack", AbilityActivation.Active, 80, new DamageEffectDefinition(500))] };
        var reset = new CombatSimulator().Simulate(Battle([source], [dirty], 130));
        var clean = reset.States.Last(frame => frame.Tick.Value == 80).Cards.Single(card => card.Id == first.EntityId);
        return clean.Haste == 0 && clean.Slow == 0 && clean.Immobilize == 0 && !clean.IsFlying && !clean.IsBerserk
            && clean.CooldownUnits.Single() == 100 && clean.CooldownDurationUnits.Single() == 100
            && clean.Values[GameAttributeKeys.AttackDamage] == 0 && clean.Values[GameAttributeKeys.Armor] == 0
            && clean.Values[GameAttributeKeys.Multicast] == 0 && clean.TransformedTags.Contains(GameTags.Beast)
            && !clean.TransformedTags.Contains(GameTags.Human) && !reset.PermanentChanges.Any()
            && reset.Events.OfType<StatusChangedEvent>().Last().Amount == 40;
    }

    internal static bool PlaybackAndRestoration()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(1, 100, new MonaHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(2, 100, new PaladinHeroDefinition());
        var wand = factory.CreateCard(new PolymorphWandCardDefinition());
        var target = factory.CreateCard(new MilitaryBootsCardDefinition(), 3);
        player.Player.Inventory.Add(wand); opponent.Player.Inventory.Add(target);
        var board = VerificationFixtures.CreateBoardService();
        if (board.PlaceCard(player, wand.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(opponent, target.Id, BoardZone.Battlefield, 3).IsFailure) return false;
        var before = MatchSnapshot.From(opponent);
        var setup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(130));
        var result = new CombatSimulator().Simulate(setup);
        var playback = new BattlePlaybackPresenter(new BattleResolution(MatchSnapshot.From(player), before, result, MatchSnapshot.From(player)));
        var initial = playback.Project(SideId.Opponent).Cards.Single();
        playback.Advance(8);
        var sheep = playback.Project(SideId.Opponent).Cards.Single();
        playback.Skip();
        var next = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(1));
        var restored = new CombatSimulator().Simulate(next).States[0].Cards.Single(card => card.Id == target.Id);
        return sheep.Id == target.Id && sheep.Level == 3 && sheep.DisplayName == "咩咩羊"
            && sheep.Key == new StringName("card.baa_sheep") && sheep.Tags.Contains(GameTags.Beast)
            && sheep.DescriptionEntries.Single().Text.Contains("1/2/3/4秒疾速")
            && sheep.Abilities.Single().Effects.Single() is ApplyStatusEffectDefinition { Amount: 30 }
            && initial.DisplayName == "军靴" && before.Cards.Single().DisplayName == "军靴"
            && MatchSnapshot.From(opponent).Cards.Single().Key == target.Attributes.Identity.Key
            && restored.TransformedIdentity is null && next.Opponent.Cards.Single().Level == 3
            && next.Opponent.Cards.Single().BoardStart == 3 && !result.PermanentChanges.Any();
    }

    private static CardBattleSetup Card(EntityId id) => new(id, 0, 0, 0, [], UseLegacyAttack: false);
    private static AbilityDefinition Ability(string suffix, AbilityActivation activation, int cooldown, params EffectDefinition[] effects) =>
        new(new StringName("verification.polymorph." + suffix), activation, AbilityTarget.SelfCard, 0, cooldown, effects);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int timeout, ulong seed = 42) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), enemy), seed, new BattleTick(timeout));
}
