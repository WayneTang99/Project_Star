using System;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 刺客匕首分级伤害、首次发动及冻结回放验证（表现层验证模块）。
internal static class AssassinDaggerChecks
{
    internal static bool LevelsAndTiming()
    {
        var definition = new AssassinDaggerCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(AssassinDaggerCardDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "刺客匕首"
            || identity.FactionKey != new StringName("valos") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Equipment) || definition.InitialLevel != 1
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        var amounts = new[] { 10, 20, 40, 80 };
        for (var level = 1; level <= 4; level++)
        {
            var config = definition.GetLevel(level)!;
            if (config.BaseCombatValues[GameAttributeKeys.AttackDamage] != amounts[level - 1]) return false;
            var card = Card(level);
            var setup = Battle([card], armor: 1, timeout: 120);
            var result = new CombatSimulator().Simulate(setup);
            var hits = Hits(result, card.EntityId);
            if (!hits.Select(hit => hit.RawDamage).SequenceEqual(new[] { amounts[level - 1] * 2, amounts[level - 1], amounts[level - 1] })
                || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 40, 80, 120 })
                || hits[0].ArmorAbsorbed != 1 || hits[0].HealthDamage != amounts[level - 1] * 2 - 1
                || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            if (new CombatSimulator().Simulate(Battle([card with { IsOnBench = true }])).Events
                .OfType<DamageDealtEvent>().Any(hit => hit.SourceKind == DamageSourceKind.Card)) return false;
        }
        return true;
    }

    internal static bool MulticastAndSnapshots()
    {
        var card = Card() with { AttackDamage = 15, Multicast = 1 };
        var berserk = new AbilityDefinition(new StringName("verification.dagger_berserk"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)]);
        var result = new CombatSimulator().Simulate(Battle([card with { Abilities = card.Abilities!.Append(berserk).ToArray() }]));
        if (!Hits(result, card.EntityId).Select(hit => hit.RawDamage).SequenceEqual(new[] { 36, 18, 18, 18 })) return false;
        var active = card.Abilities!.Single();
        var gemAbility = new AbilityDefinition(active.Key, active.Activation, active.Target, 0, 40,
            active.Effects.Append(new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Poison, 50)).ToArray());
        card = card with { Abilities = [gemAbility] };
        var setup = Battle([card]);
        var simulator = new CombatSimulator();
        var before = simulator.Preview(setup).Cards.Single();
        if (before.Values[GameAttributeKeys.AttackDamage] != 15 || before.EffectAmounts[0] != 30
            || before.EffectAmounts[1] != 15) return false;
        result = simulator.Simulate(setup);
        if (!Hits(result, card.EntityId).Select(hit => hit.RawDamage).SequenceEqual(new[] { 30, 15, 15, 15 })) return false;
        var first = result.States.Last(state => state.Tick.Value == 40).Cards.Single();
        return first.Values[GameAttributeKeys.AttackDamage] == 15 && first.EffectAmounts[0] == 15
            && first.EffectAmounts[1] == 7 && first.Side == SideId.Player
            && result.States.Last(state => state.Tick.Value == 40).Opponent.Poison == 22
            && before.EffectAmounts[0] == 30;
    }

    internal static bool IsolationAndValidation()
    {
        var first = Card(); var second = Card() with { BoardStart = 1 };
        var result = new CombatSimulator().Simulate(Battle([first, second]));
        if (!Hits(result, first.EntityId).Select(hit => hit.RawDamage).SequenceEqual(new[] { 20, 10 })
            || !Hits(result, second.EntityId).Select(hit => hit.RawDamage).SequenceEqual(new[] { 20, 10 })) return false;
        var mirrored = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), []),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), [first]),
            42, new BattleTick(80), new BattleTick(81));
        if (!Hits(new CombatSimulator().Simulate(mirrored), first.EntityId).Select(hit => hit.RawDamage)
            .SequenceEqual(new[] { 20, 10 })) return false;
        var active = first.Abilities!.Single();
        var manaAbility = new AbilityDefinition(active.Key, active.Activation, active.Target, 10, 40, active.Effects);
        var delayed = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 1), [first with { Abilities = [manaAbility] }]),
            mirrored.Player, 42, new BattleTick(120), new BattleTick(121));
        var delayedHit = Hits(new CombatSimulator().Simulate(delayed), first.EntityId).Single();
        if (delayedHit.RawDamage != 20 || delayedHit.Tick.Value != 100) return false;
        foreach (var (multiplier, activation) in new[] { (0, AbilityActivation.Active), (2, AbilityActivation.PassiveOnBattleStart) })
        {
            try
            {
                _ = new AbilityDefinition(new StringName("verification.invalid_first_damage"), activation,
                    AbilityTarget.EnemyHero, 0, activation == AbilityActivation.Active ? 40 : 0,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage, FirstActivationMultiplier: multiplier)]);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Card(int level = 1)
    {
        var config = new AssassinDaggerCardDefinition().GetLevel(level)!;
        return new CardBattleSetup(EntityId.New(), 0, config.BaseCombatValues[GameAttributeKeys.AttackDamage], 40,
            config.Abilities, UseLegacyAttack: false);
    }

    private static BattleSetup Battle(CardBattleSetup[] cards, int armor = 0, int timeout = 80) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), cards),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, armor, ManaRegen: 0), []),
            42, new BattleTick(timeout), new BattleTick(timeout + 1));

    private static DamageDealtEvent[] Hits(BattleResult result, EntityId id) =>
        result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == id && hit.SourceKind == DamageSourceKind.Card).ToArray();
}
