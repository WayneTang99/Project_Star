using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 双截棍分级、多重与相邻充能行为验证（表现层）。
internal static class NunchakuChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new NunchakuCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.nunchaku") || identity.DisplayName != "双截棍"
            || identity.FactionKey != new StringName("jiyun") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(NunchakuChecks).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        for (var level = 2; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var attributes = instance.Attributes.BaseCombat;
            if (attributes.GetFinalValue(GameAttributeKeys.AttackDamage) != 5
                || attributes.GetFinalValue(GameAttributeKeys.CooldownTicks) != 70
                || attributes.GetFinalValue(GameAttributeKeys.Multicast) != 1) return false;
            var weapon = new CardBattleSetup(instance.Id, 3, 5, 70, instance.Abilities,
                UseLegacyAttack: false, Tags: instance.Tags, ElementKeys: identity.ElementKeys)
                { Level = level, Multicast = 1 };
            var solo = Run([weapon], [], 72);
            var hits = solo.Events.OfType<DamageDealtEvent>().ToArray();
            if (hits.Length != 2 || hits.Any(e => e.RawDamage != 5) || !hits.Select(e => e.Tick.Value).SequenceEqual(new long[] { 70, 72 })
                || solo.Events.OfType<CardChargedEvent>().Any()) return false;
            // 左侧大型辅助卡与右侧不同标签卡均可触发；多重逐次充能。
            var left = Neighbor(0) with { OccupiedSlots = 3, Multicast = 1 };
            var right = Neighbor(4);
            foreach (var mirror in new[] { false, true })
            {
                var result = Run([left, weapon, right], [Neighbor(4)], 24, mirror);
                var charges = result.Events.OfType<CardChargedEvent>().ToArray();
                if (charges.Length != 3 || charges.Any(e => e.TargetCardId != weapon.EntityId
                        || e.AmountTicks != (level - 1) * 10)
                    || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()) return false;
                if (level >= 3 && result.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == weapon.EntityId) != 2)
                    return false;
            }
            foreach (var excluded in new[] { left with { OccupiedSlots = 2 }, right with { BoardStart = 5 },
                left with { IsOnBench = true } })
                if (Run([excluded, weapon], [Neighbor(4)], 20).Events.OfType<CardChargedEvent>().Any()) return false;
            if (Run([left, weapon with { IsOnBench = true }, right], [], 20).Events.OfType<CardChargedEvent>().Any())
                return false;
            var setup = Battle([left, weapon, right], [], 70);
            var simulator = new CombatSimulator();
            if (!simulator.Simulate(setup).Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition("verification.nunchaku.neighbor", AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 20, [new HealEffectDefinition(1)])],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Beast]));
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool mirror = false)
    {
        var setup = Battle(cards, enemies, ticks);
        return new CombatSimulator().Simulate(mirror
            ? new BattleSetup(setup.Opponent, setup.Player, 42, new BattleTick(ticks), new BattleTick(ticks + 1)) : setup);
    }
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
