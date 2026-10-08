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
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 两种传动齿轮的分级、方向、充能队列及回响生命周期验证（表现层）。
internal static class TransmissionGearChecks
{
    internal static bool LevelsAndEffects()
    {
        var catalog = DefinitionRegistry.Scan(typeof(TransmissionGearChecks).Assembly);
        foreach (var (definition, key, name, size, initial) in new (CardDefinition, string, string, CardSize, int)[]
        {
            (new TransmissionGearCardDefinition(), "card.transmission_gear", "传动齿轮", CardSize.Small, 1),
            (new TransmissionGearAssemblyCardDefinition(), "card.transmission_gear_assembly", "传动齿轮组", CardSize.Medium, 3),
        })
        {
            var identity = definition.Attributes.Identity;
            var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
            if (!catalog.Cards.ContainsKey(new StringName(key)) || identity.Key != new StringName(key)
                || identity.DisplayName != name || identity.Size != size || identity.FactionKey != new StringName("harla")
                || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
                || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Mechanical)
                || definition.InitialLevel != initial || definition.SupportsLevel(5)
                || initial == 3 && (definition.SupportsLevel(1) || definition.SupportsLevel(2))
                || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Echo
                || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * (int)size) return false;
            for (var level = initial; level <= 4; level++)
            {
                var instance = new EntityFactory().CreateCard(definition, level);
                var frozenAbilities = MatchSnapshot.CopyAbilities(instance.Abilities);
                if (frozenAbilities.Single().TriggerCardSide != AdjacentCardSide.Left
                    || frozenAbilities.Single().Target != AbilityTarget.RightAdjacentAlliedCard) return false;
                if (instance.Abilities.Any(ability => ability.Activation == AbilityActivation.Active
                    || ability.ManaCost != 0 || ability.CooldownTicks != 0)) return false;
                var gear = Gear(definition, level, 3) with { Abilities = frozenAbilities };
                var left = ActiveCard(0, 20) with { OccupiedSlots = 3, Multicast = 1 };
                var right = ActiveCard(3 + (int)size, 100) with { OccupiedSlots = 2 };
                var setup = Battle([left, gear, right], [], 20);
                var simulator = new CombatSimulator();
                var result = simulator.Simulate(setup);
                var frozenRight = result.States[^1].Cards.Single(card => card.Id == right.EntityId);
                var enemyResult = simulator.Simulate(Battle([], [left, gear, right], 20));
                var enemyRight = enemyResult.States[^1].Cards.Single(card => card.Id == right.EntityId);
                if (EchoCount(enemyResult, gear) != 2 || enemyRight.Haste != frozenRight.Haste
                    || !enemyRight.CooldownUnits.SequenceEqual(frozenRight.CooldownUnits)) return false;
                if (EchoCount(result, gear) != 2 || result.PermanentChanges.Count != 0
                    || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)
                    || result.States[^1].Cards.Where(card => card.Id != right.EntityId).Any(card => card.Haste != 0)
                    || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 0) return false;
                if (size == CardSize.Small)
                {
                    if (frozenRight.Haste != level * 20 || frozenRight.CooldownUnits.Single() != 160
                        || result.Events.OfType<StatusChangedEvent>().Count(e => e.Amount == level * 10) != 2) return false;
                }
                else
                {
                    var charges = result.Events.OfType<CardChargedEvent>().ToArray();
                    if (frozenRight.CooldownUnits.Single() != 160 - (level - 2) * 40
                        || charges.Length != 2 || charges.Any(e => e.SourceCardId != gear.EntityId
                            || e.TargetCardId != right.EntityId || e.AmountTicks != (level - 2) * 10)) return false;
                }
                if (CardDisplayAdapter.AbilityDetails(instance.Abilities).Contains("ChargeCardEffectDefinition")) return false;
            }
        }
        return true;
    }

    internal static bool DirectionAndLifetime()
    {
        foreach (var definition in new CardDefinition[] { new TransmissionGearCardDefinition(), new TransmissionGearAssemblyCardDefinition() })
        {
            var gear = Gear(definition, definition.InitialLevel, 3);
            var left = ActiveCard(0, 20) with { OccupiedSlots = 3 };
            var right = ActiveCard(3 + gear.OccupiedSlots, 100);
            var enemy = ActiveCard(0, 20) with { OccupiedSlots = 3 };
            var startPassive = new AbilityDefinition(new StringName("verification.gear.start"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.AlliedHero, 0, 0, [new HealEffectDefinition(1)]);
            // 非左邻、失败发动、战斗开始及回响均不触发；辅助发动正常触发。
            foreach (var excluded in new[]
            {
                left with { OccupiedSlots = 2 }, left with { IsOnBench = true },
                left with { Abilities = [Active(20, 1)] }, left with { Abilities = [startPassive] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.gear.echo"),
                    AbilityActivation.EchoOnAbilityActivated, AbilityTarget.AlliedHero, 0, 0, [new HealEffectDefinition(1)])] },
            })
                if (EchoCount(Simulate([excluded, gear, right], [enemy], 20), gear) != 0) return false;
            if (EchoCount(Simulate([gear, right with { Abilities = [Active(20)] }], [enemy], 20), gear) != 0) return false;
            foreach (var inactive in new[] { gear with { IsOnBench = true }, DestroyAtStart(gear) })
                if (EchoCount(Simulate([left, inactive, right], [], 20), gear) != 0) return false;
            // 空位不跨越，备战与摧毁目标不接收效果，敌方同位置也不接收。
            foreach (var unavailable in new[] { right with { BoardStart = right.BoardStart + 1 },
                right with { IsOnBench = true }, DestroyAtStart(right) })
            {
                var result = Simulate([left, gear, unavailable], [right with { EntityId = EntityId.New() }], 20);
                if (EchoCount(result, gear) != 1 || HasTransferredEffect(result)) return false;
            }
            var noTarget = Simulate([left, gear], [], 20);
            if (EchoCount(noTarget, gear) != 1 || HasTransferredEffect(noTarget)) return false;
            if (EchoCount(Simulate([gear with { BoardStart = 0 }, ActiveCard(gear.OccupiedSlots, 20)], [], 20), gear) != 0)
                return false;
        }
        var first = Gear(new TransmissionGearCardDefinition(), 1, 1);
        var second = Gear(new TransmissionGearCardDefinition(), 1, 2);
        var stopped = Simulate([ActiveCard(0, 20), first, second, ActiveCard(3, 100)], [], 20);
        return EchoCount(stopped, first) == 1 && EchoCount(stopped, second) == 0
            && stopped.States[^1].Cards.Single(card => card.Id == second.EntityId).Haste == 10;
    }

    internal static bool CooldownAndQueue()
    {
        var gear = Gear(new TransmissionGearCardDefinition(), 1, 1);
        var right = ActiveCard(2, 40);
        var haste = Simulate([ActiveCard(0, 20), gear, right], [], 30);
        if (haste.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == right.EntityId).Tick.Value != 30
            || haste.States[^1].Cards.Single(card => card.Id == right.EntityId).Haste != 0) return false;
        for (var level = 3; level <= 4; level++)
        {
            var assembly = Gear(new TransmissionGearAssemblyCardDefinition(), level, 1);
            var charged = ActiveCard(3, 25);
            var result = Simulate([ActiveCard(0, 20) with { Multicast = 1 }, assembly, charged], [], 20);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == charged.EntityId).ToArray();
            if (activations.Length != 1 || activations[0].Tick.Value != 20 || activations[0].IsEcho
                || result.States[^1].Cards.Single(card => card.Id == charged.EntityId).CooldownUnits.Single() != 50) return false;
        }
        // 充能至零后的正常发动仍可驱动下一张齿轮。
        var source = Gear(new TransmissionGearAssemblyCardDefinition(), 3, 1);
        var receiver = ActiveCard(3, 25);
        var next = Gear(new TransmissionGearCardDefinition(), 1, 4);
        var end = ActiveCard(5, 100);
        var chain = Simulate([ActiveCard(0, 20), source, receiver, next, end], [], 20);
        if (EchoCount(chain, next) != 1 || chain.States[^1].Cards.Single(card => card.Id == end.EntityId).Haste != 10) return false;
        // 通用机制同样支持右侧发动、左侧目标。
        var reverse = gear with { Abilities = [new AbilityDefinition(new StringName("verification.gear.reverse"),
            AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.LeftAdjacentAlliedCard, 0, 0,
            [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)], triggerCardSide: AdjacentCardSide.Right)] };
        var leftTarget = ActiveCard(0, 100);
        return Simulate([leftTarget, reverse, ActiveCard(2, 20)], [], 20).States[^1].Cards.Single(card => card.Id == leftTarget.EntityId).Haste == 10;
    }

    internal static bool ConfigurationBoundaries()
    {
        Action[] invalid =
        [
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.EchoOnAdjacentAlliedCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)]),
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.EchoOnAdjacentAlliedCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)], triggerCardSide: (AdjacentCardSide)2),
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.EchoOnAdjacentAlliedCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)], allowsBench: true, triggerCardSide: AdjacentCardSide.Left),
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.RightAdjacentAlliedCard, 0, 0, [new DamageEffectDefinition(1)]),
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.RightAdjacentAlliedCard, 0, 0, [new ChargeCardEffectDefinition(0)]),
            () => _ = new AbilityDefinition("verification.gear.invalid", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new ChargeCardEffectDefinition(10)]),
            () => SkillDefinition.ValidateNonCardAbilities(new TransmissionGearCardDefinition().GetLevel(1)!.Abilities),
            () => SkillDefinition.ValidateNonCardAbilities(new TransmissionGearAssemblyCardDefinition().GetLevel(3)!.Abilities),
        ];
        foreach (var action in invalid)
        {
            try { action(); return false; }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Gear(CardDefinition definition, int level, int start) =>
        new(EntityId.New(), start, 0, 0, definition.GetLevel(level)!.Abilities, UseLegacyAttack: false,
            Tags: definition.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size, ElementKeys: [GameElements.General]) { Level = level };
    private static AbilityDefinition Active(int ticks, int mana = 0) => new(new StringName("verification.gear.active"),
        AbilityActivation.Active, AbilityTarget.AlliedHero, mana, ticks, [new HealEffectDefinition(1)]);
    private static CardBattleSetup ActiveCard(int start, int ticks) => new(EntityId.New(), start, 0, ticks, [Active(ticks)], UseLegacyAttack: false);
    private static CardBattleSetup DestroyAtStart(CardBattleSetup card) => card with
    {
        Abilities = card.Abilities!.Append(new AbilityDefinition(new StringName("verification.gear.destroy"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray(),
    };
    private static int EchoCount(BattleResult result, CardBattleSetup gear) =>
        result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == gear.EntityId && e.IsEcho);
    private static bool HasTransferredEffect(BattleResult result) => result.Events.OfType<CardChargedEvent>().Any()
        || result.States[^1].Cards.Any(card => card.Haste != 0);
    private static BattleResult Simulate(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new CombatSimulator().Simulate(Battle(cards, enemies, ticks));
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
