using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 钉头页锤分级伤害与护甲条件实时变化验证（表现层验证模块）。
internal static class FlangedMaceChecks
{
    internal static bool LevelsAndArmor()
    {
        var definition = new FlangedMaceCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "钉头页锤" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Equipment) || definition.InitialLevel != 1) return false;
        var amounts = new[] { 10, 20, 40, 80 };
        for (var level = 1; level <= 4; level++)
        {
            var configuration = definition.GetLevel(level)!;
            var damage = configuration.BaseCombatValues[GameAttributeKeys.AttackDamage];
            if (damage != amounts[level - 1]) return false;
            var card = new CardBattleSetup(EntityId.New(), 0, damage, 50, configuration.Abilities, UseLegacyAttack: false);
            foreach (var armor in new[] { 0, 1, 1000 })
            {
                var setup = Battle(card, armor, 50);
                var result = new CombatSimulator().Simulate(setup);
                var hit = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Single();
                var expected = damage * (armor > 0 ? 2 : 1);
                if (hit.Tick.Value != 50 || hit.RawDamage != expected
                    || hit.ArmorAbsorbed != System.Math.Min(armor, expected)
                    || hit.HealthDamage != expected - hit.ArmorAbsorbed
                    || result.Events.OfType<ManaChangedEvent>().Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var bench = new CombatSimulator().Simulate(Battle(card with { IsOnBench = true }, 1, 50));
            if (bench.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Any()) return false;
        }
        return true;
    }

    internal static bool DynamicArmorAndBonuses()
    {
        var abilities = new FlangedMaceCardDefinition().GetLevel(1)!.Abilities;
        var card = new CardBattleSetup(EntityId.New(), 0, 10, 50, abilities, UseLegacyAttack: false);
        var rearm = new CardBattleSetup(EntityId.New(), 0, 0, 75,
            [new AbilityDefinition(new StringName("verification.rearm_once"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 75,
                [new GainSourceHeroArmorEffectDefinition(1), new DestroyCardEffectDefinition(false)])],
            UseLegacyAttack: false);
        var dynamic = new CombatSimulator().Simulate(Battle(card, 1, 150, [rearm]));
        if (!dynamic.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Select(hit => hit.RawDamage)
            .SequenceEqual(new[] { 20, 20, 10 })) return false;
        // 属性加成与多重：护甲被首击打破后，同组额外发动立即恢复原倍率。
        var boosted = card with { AttackDamage = 15, Multicast = 1 };
        var result = new CombatSimulator().Simulate(Battle(boosted, 1, 50));
        if (!result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Select(hit => hit.RawDamage)
            .SequenceEqual(new[] { 30, 15 })) return false;
        var berserk = new AbilityDefinition(new StringName("verification.mace_berserk"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)]);
        result = new CombatSimulator().Simulate(Battle(boosted with { Abilities = abilities.Append(berserk).ToArray() }, 1, 50));
        if (!result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Select(hit => hit.RawDamage)
            .SequenceEqual(new[] { 36, 18 })) return false;
        var mirrored = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 1, ManaRegen: 0), []),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), [card]),
            42, new BattleTick(50), new BattleTick(51));
        return new CombatSimulator().Simulate(mirrored).Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).Single().RawDamage == 20;
    }

    private static BattleSetup Battle(CardBattleSetup card, int armor, int timeout, CardBattleSetup[]? enemy = null) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), [card]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, armor, ManaRegen: 0), enemy ?? []),
            42, new BattleTick(timeout), new BattleTick(timeout + 1));
}
