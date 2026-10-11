using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 辉耀巨龙分级伤害、灼伤与状态冷却光环验证（表现层验证模块）。
internal static class RadiantDragonChecks
{
    internal static bool IdentityAndDamage()
    {
        var definition = new RadiantDragonCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.radiant_dragon") || identity.DisplayName != "辉耀巨龙"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire, GameElements.Light })
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Tags.Count != 3 || !definition.Tags.Contains(GameTags.Large)
            || !definition.Tags.Contains(GameTags.Mount) || !definition.Tags.Contains(GameTags.Dragon)
            || TagDisplayNames.Get(GameTags.Dragon) != "龙族"
            || !identity.DescriptionEntries.Select(entry => entry.KeywordKey)
                .SequenceEqual(new[] { CardKeywords.Activate, CardKeywords.Aura })
            || !DefinitionRegistry.Scan(typeof(RadiantDragonCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var bonus in new[] { 0, 37 })
        {
            var card = Card(level);
            var baseDamage = level == 3 ? 100 : 200;
            if (card.AttackDamage != baseDamage || card.CooldownTicks != 80
                || card.Abilities!.Count != 2 || card.Abilities.Any(ability => ability.ManaCost != 0)) return false;
            var setup = Battle([card with { AttackDamage = baseDamage + bonus }], 80, 1000);
            var result = new CombatSimulator().Simulate(setup);
            var hit = result.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == card.EntityId);
            var burn = result.Events.OfType<StatusChangedEvent>().Single();
            if (hit.Tick.Value != 80 || hit.RawDamage != baseDamage + bonus || hit.HealthDamage != 0
                || hit.ArmorAbsorbed != baseDamage + bonus || burn.Status != BattleStatus.Burn
                || burn.Amount != (baseDamage + bonus) / 10
                || result.States[^1].Cards.Single().IsFlying || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        var repeated = Card(3) with { Multicast = 1 };
        var stacked = new CombatSimulator().Simulate(Battle([repeated], 80));
        return stacked.Events.OfType<StatusChangedEvent>().Select(item => item.Amount).SequenceEqual(new[] { 10, 10 })
            && stacked.States[^1].Opponent.Burn == 20 && stacked.PermanentAttributeBonuses.Count == 0;
    }

    internal static bool FlyingTransitions()
    {
        var card = Card(3);
        var trainer = new CardBattleSetup(EntityId.New(), 3, 0, 20,
            [new AbilityDefinition(new StringName("verification.dragon.flight"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 20,
                [new SetRandomAdjacentAlliedTaggedCardStateEffectDefinition(GameTags.Mount, GameAttributeKeys.Flying, true)])],
            UseLegacyAttack: false);
        var setup = Battle([card, trainer], 80);
        var entered = new CombatSimulator().Simulate(setup);
        var atEntry = State(entered, card.EntityId, 20);
        if (!atEntry.IsFlying || atEntry.CooldownUnits.Single() != 40 || atEntry.CooldownDurationUnits.Single() != 80
            || !Hits(entered, card.EntityId).SequenceEqual(new long[] { 40, 80 })
            || entered.Events.OfType<CardStateChangedEvent>().Count(item => item.CardId == card.EntityId) != 1
            || !entered.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        var flying = WithOpeningState(card, GameAttributeKeys.Flying);
        var landed = flying with { Abilities = flying.Abilities!.Append(new AbilityDefinition(
            new StringName("verification.dragon.land"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 90,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, false)])).ToArray() };
        var exiting = new CombatSimulator().Simulate(Battle([landed], 120));
        var atExit = State(exiting, card.EntityId, 50);
        if (atExit.IsFlying || atExit.CooldownUnits[0] != 140 || atExit.CooldownDurationUnits[0] != 160
            || !Hits(exiting, card.EntityId).SequenceEqual(new long[] { 40, 120 })) return false;
        var bench = new CombatSimulator().Simulate(Battle([card with { IsOnBench = true }, trainer], 80));
        if (Hits(bench, card.EntityId).Any() || State(bench, card.EntityId, 80).IsFlying) return false;
        var nextBattle = new CombatSimulator().Simulate(Battle([card], 80));
        return !State(nextBattle, card.EntityId, 0).IsFlying
            && Hits(nextBattle, card.EntityId).SequenceEqual(new long[] { 80 })
            && State(entered, card.EntityId, 0).CooldownDurationUnits.Single() == 160;
    }

    internal static bool AuraCompositionAndLimits()
    {
        var card = WithOpeningState(Card(4), GameAttributeKeys.Flying) with { CooldownMultiplier = .5m };
        var provider = new CardBattleSetup(EntityId.New(), 3, 0, 0,
            [new AbilityDefinition(new StringName("verification.dragon.element_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new ReduceAlliedElementCardCooldownAuraEffectDefinition(GameElements.Fire, 50)])], UseLegacyAttack: false);
        var composed = new CombatSimulator().Simulate(Battle([card, provider], 20));
        if (!Hits(composed, card.EntityId).SequenceEqual(new long[] { 10, 20 })
            || State(composed, card.EntityId, 0).CooldownDurationUnits.Single() != 20) return false;
        var destroyed = card with { Abilities = card.Abilities!.Append(new AbilityDefinition(
            new StringName("verification.dragon.destroy"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var removed = new CombatSimulator().Simulate(Battle([destroyed], 20));
        if (Hits(removed, card.EntityId).Any() || State(removed, card.EntityId, 0).CooldownDurationUnits.Single() != 80) return false;
        // 复用同一光环机制处理狂暴状态，缩减超过基础冷却时最低为0。
        var shortCard = Card(3);
        var stateAura = new AbilityDefinition(new StringName("verification.dragon.state_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0, [new ReduceSourceCooldownWhileInStateEffectDefinition(GameAttributeKeys.Berserk, 100)]);
        shortCard = WithOpeningState(shortCard with { Abilities = [shortCard.Abilities![0], stateAura] }, GameAttributeKeys.Berserk);
        var capped = new CombatSimulator().Simulate(Battle([shortCard], 3));
        if (!Hits(capped, shortCard.EntityId).SequenceEqual(new long[] { 1, 2, 3 })
            || capped.States.SelectMany(frame => frame.Cards).Any(snapshot => snapshot.CooldownDurationUnits.Any(value => value < 0))) return false;
        foreach (var invalid in new[]
        {
            (GameAttributeKeys.Flying, 0, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, false),
            (GameAttributeKeys.AttackDamage, 40, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, false),
            (GameAttributeKeys.Flying, 40, AbilityActivation.Active, AbilityTarget.SelfCard, false),
            (GameAttributeKeys.Flying, 40, AbilityActivation.PassiveAura, AbilityTarget.EnemyHero, false),
            (GameAttributeKeys.Flying, 40, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, true),
        })
        {
            try
            {
                _ = new AbilityDefinition(new StringName("verification.dragon.invalid"), invalid.Item3, invalid.Item4, 0, 0,
                    [new ReduceSourceCooldownWhileInStateEffectDefinition(invalid.Item1, invalid.Item2)], allowsBench: invalid.Item5);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Card(int level)
    {
        var instance = new EntityFactory().CreateCard(new RadiantDragonCardDefinition(), level);
        return new CardBattleSetup(instance.Id, 0, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), instance.Abilities,
            Tags: instance.Tags, OccupiedSlots: 3, ElementKeys: instance.Attributes.Identity.ElementKeys) { Level = level };
    }

    private static CardBattleSetup WithOpeningState(CardBattleSetup card, StringName state) => card with
    {
        Abilities = card.Abilities!.Append(new AbilityDefinition(new StringName("verification.dragon.opening_state"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(state, true)])).ToArray(),
    };

    private static long[] Hits(BattleResult result, EntityId id) => result.Events.OfType<DamageDealtEvent>()
        .Where(item => item.SourceCardId == id).Select(item => item.Tick.Value).ToArray();
    private static CardBattleSnapshot State(BattleResult result, EntityId id, long tick) =>
        result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(card => card.Id == id);
    private static BattleSetup Battle(CardBattleSetup[] cards, int timeout, int enemyArmor = 0) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, enemyArmor, ManaRegen: 0), []),
        42, new BattleTick(timeout), new BattleTick(timeout + 1));
}
