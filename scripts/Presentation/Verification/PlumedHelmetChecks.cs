using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 羽饰头盔分级、相邻人类筛选与自身充能验证（表现层）。
internal static class PlumedHelmetChecks
{
    internal static bool LevelsAndDamage()
    {
        var definition = new PlumedHelmetCardDefinition();
        var identity = definition.Attributes.Identity;
        var catalog = DefinitionRegistry.Scan(typeof(PlumedHelmetChecks).Assembly);
        var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
        if (!catalog.Cards.ContainsKey(identity.Key) || identity.Key != new StringName("card.plumed_helmet")
            || identity.DisplayName != "羽饰头盔" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !identity.DescriptionEntries.Select(entry => entry.KeywordKey).SequenceEqual(new[] { CardKeywords.Activate, CardKeywords.Echo })
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        for (var level = 2; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var expected = 20 << (level - 2);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != expected
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 50) return false;
            var helmet = Helmet(level) with { Abilities = MatchSnapshot.CopyAbilities(instance.Abilities) };
            foreach (var armor in new[] { 0, 10, 100 })
            {
                var result = Simulate([helmet], [], 50, armor);
                var damage = result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceKind == DamageSourceKind.Card);
                if (damage.Tick.Value != 50 || damage.RawDamage != expected
                    || damage.ArmorAbsorbed != Math.Min(expected, armor) || damage.HealthDamage != expected - damage.ArmorAbsorbed
                    || result.Events.OfType<ManaChangedEvent>().Any()) return false;
            }
            var boosted = Simulate([helmet with { AttackDamage = expected + 5, Multicast = 1 }], [], 50);
            if (!boosted.Events.OfType<DamageDealtEvent>().Where(e => e.SourceKind == DamageSourceKind.Card)
                .Select(e => e.RawDamage).SequenceEqual(new[] { expected + 5, expected + 5 })) return false;
        }
        return true;
    }

    internal static bool AdjacentHumanFilters()
    {
        var helmet = Helmet(2);
        var left = Human(0, 20) with { OccupiedSlots = 3, Multicast = 1 };
        var right = Human(4, 20) with { OccupiedSlots = 2 };
        var setup = Battle([left, helmet, right], [Human(0, 20)], 20);
        var simulator = new CombatSimulator();
        var result = simulator.Simulate(setup);
        var charges = result.Events.OfType<CardChargedEvent>().ToArray();
        if (charges.Length != 3 || charges.Any(e => e.SourceCardId != helmet.EntityId
                || e.TargetCardId != helmet.EntityId || e.AmountTicks != 10)
            || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == helmet.EntityId && !e.IsEcho) != 1
            || result.States[^1].Cards.Single(e => e.Id == helmet.EntityId).CooldownUnits.Single() != 100
            || result.PermanentChanges.Count != 0 || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
        var mirrored = new BattleSetup(setup.Opponent, setup.Player, 42, new BattleTick(20), new BattleTick(21));
        if (simulator.Simulate(mirrored).Events.OfType<CardChargedEvent>().Count() != 3) return false;
        foreach (var excluded in new[]
        {
            left with { Tags = new TagSet([GameTags.Equipment]) }, left with { OccupiedSlots = 2 },
            left with { IsOnBench = true }, DestroyAtStart(left),
            left with { Abilities = [Active(20, 1)] },
            left with { Abilities = [new AbilityDefinition("verification.helmet.start", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.AlliedHero, 0, 0, [new HealEffectDefinition(1)])] },
            left with { Abilities = [new AbilityDefinition("verification.helmet.echo", AbilityActivation.EchoOnAbilityActivated,
                AbilityTarget.AlliedHero, 0, 0, [new HealEffectDefinition(1)])] },
        })
            if (Simulate([excluded, helmet], [Human(0, 20)], 20).Events.OfType<CardChargedEvent>().Any()) return false;
        foreach (var excluded in new[] { right with { BoardStart = 5 }, right with { Tags = new TagSet([GameTags.Beast]) } })
            if (Simulate([helmet, excluded], [], 20).Events.OfType<CardChargedEvent>().Any()) return false;
        foreach (var inactive in new[] { helmet with { IsOnBench = true }, DestroyAtStart(helmet) })
            if (Simulate([left, inactive, right], [], 20).Events.OfType<CardChargedEvent>().Any()) return false;
        return true;
    }

    internal static bool ChargeTimingAndBoundaries()
    {
        var helmet = Helmet(2);
        // 每次相邻人类发动扣减冷却，第二次发动在4秒将头盔充能至零。
        var result = Simulate([Human(0, 20) with { OccupiedSlots = 3 }, helmet], [], 40);
        if (result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == helmet.EntityId && !e.IsEcho).Tick.Value != 40)
            return false;
        // 同Tick已就绪的头盔不会因重复充能重复排队，充能只作用于主动能力。
        var ready = Simulate([Human(0, 50) with { OccupiedSlots = 3, Multicast = 2 }, helmet], [], 50);
        if (ready.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == helmet.EntityId && !e.IsEcho) != 1)
            return false;
        // 标签和方向随冻结配置保留；不接受空标签或元素筛选。
        var abilities = MatchSnapshot.CopyAbilities(new PlumedHelmetCardDefinition().GetLevel(2)!.Abilities);
        if (abilities.Skip(1).Any(a => a.TriggerCardTag != GameTags.Human)
            || !abilities.Skip(1).Select(a => a.TriggerCardSide).SequenceEqual(new AdjacentCardSide?[] { AdjacentCardSide.Left, AdjacentCardSide.Right }))
            return false;
        foreach (var invalidTag in new StringName?[] { new StringName("") })
        {
            try
            {
                _ = new AbilityDefinition("verification.helmet.invalid", AbilityActivation.EchoOnAdjacentAlliedCardActivated,
                    AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)],
                    triggerCardTag: invalidTag, triggerCardSide: AdjacentCardSide.Left);
                return false;
            }
            catch (ArgumentException) { }
        }
        try
        {
            _ = new AbilityDefinition("verification.helmet.invalid", AbilityActivation.EchoOnAdjacentAlliedCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)],
                triggerCardElement: GameElements.General, triggerCardSide: AdjacentCardSide.Left);
            return false;
        }
        catch (ArgumentException) { }
        return true;
    }

    private static CardBattleSetup Helmet(int level)
    {
        var definition = new PlumedHelmetCardDefinition();
        return new CardBattleSetup(EntityId.New(), 3, 20 << (level - 2), 50, definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: definition.Tags, ElementKeys: [GameElements.General]) { Level = level };
    }
    private static AbilityDefinition Active(int ticks, int mana = 0) => new("verification.helmet.human",
        AbilityActivation.Active, AbilityTarget.AlliedHero, mana, ticks, [new HealEffectDefinition(1)]);
    private static CardBattleSetup Human(int start, int ticks) => new(EntityId.New(), start, 0, ticks,
        [Active(ticks)], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]));
    private static CardBattleSetup DestroyAtStart(CardBattleSetup card) => card with
    {
        Abilities = card.Abilities!.Append(new AbilityDefinition("verification.helmet.destroy",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray(),
    };
    private static BattleResult Simulate(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, int armor = 0) =>
        new CombatSimulator().Simulate(Battle(cards, enemies, ticks, armor));
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, int armor = 0) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, armor, MaxMana: 0, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
