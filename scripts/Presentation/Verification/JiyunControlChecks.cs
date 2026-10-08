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

// 极云治疗、迟缓充能、木属性回响及双方攻击禁锢验证（表现层）。
internal static class JiyunControlChecks
{
    internal static bool IdentityAndLevels()
    {
        var registry = DefinitionRegistry.Scan(typeof(JiyunControlChecks).Assembly);
        foreach (var (definition, name, key, size, element, initial, tags) in new
            (CardDefinition, string, string, CardSize, StringName, int, StringName[])[]
        {
            (new WineGourdCardDefinition(), "酒葫芦", "card.wine_gourd", CardSize.Small, GameElements.Wood, 1, []),
            (new BambooHatCardDefinition(), "斗笠", "card.bamboo_hat", CardSize.Medium, GameElements.General, 1, [GameTags.Equipment]),
            (new HiddenBambooGroveCardDefinition(), "隐秘竹林", "card.hidden_bamboo_grove", CardSize.Large, GameElements.Wood, 2, [GameTags.Plant, GameTags.Location]),
            (new ZenLightTempleCardDefinition(), "禅光寺", "card.zen_light_temple", CardSize.Large, GameElements.General, 4, [GameTags.Location]),
        })
        {
            var identity = definition.Attributes.Identity;
            var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
            if (!registry.Cards.ContainsKey(new StringName(key)) || identity.Key != new StringName(key)
                || identity.DisplayName != name || identity.FactionKey != new StringName("jiyun") || identity.Size != size
                || !identity.ElementKeys.SequenceEqual(new[] { element }) || definition.InitialLevel != initial
                || definition.Tags.Count != tags.Length + 1 || tags.Any(tag => !definition.Tags.Contains(tag))
                || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * (int)size) return false;
            for (var level = 1; level <= 5; level++)
            {
                if (definition.SupportsLevel(level) != (level >= initial && level <= 4)) return false;
                if (!definition.SupportsLevel(level)) continue;
                var instance = new EntityFactory().CreateCard(definition, level);
                var frozen = MatchSnapshot.CopyAbilities(instance.Abilities);
                if (frozen.Any(ability => ability.ManaCost != 0)
                    || !frozen.Select(ability => (ability.Activation, ability.Target, ability.TriggerCardElement))
                        .SequenceEqual(instance.Abilities.Select(ability => (ability.Activation, ability.Target, ability.TriggerCardElement)))
                    || CardDisplayAdapter.AbilityDetails(frozen).Contains("EffectDefinition")) return false;
                if (initial >= 2 && (frozen.Any(ability => ability.Activation == AbilityActivation.Active || ability.CooldownTicks != 0)
                    || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 0)) return false;
            }
        }
        return true;
    }

    internal static bool GourdHealingAndSlow()
    {
        int[] healing = [40, 80, 160, 360];
        for (var level = 1; level <= 4; level++)
        {
            var gourd = Card(new WineGourdCardDefinition(), level, 0);
            var opening = ActiveCard(0, 100, new DamageEffectDefinition(800)) with
            {
                Abilities = [new AbilityDefinition("verification.jiyun.opening", AbilityActivation.PassiveOnBattleStart,
                    AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(800)])],
            };
            var result = Simulate([gourd], [opening], 30);
            if (result.States.Last(frame => frame.Tick.Value == 30 && frame.Player.Health > 0).Player.Health != 200 + healing[level - 1]
                || Snapshot(result, gourd).Slow != 10 || Snapshot(result, opening).Slow != 0 || EchoCount(result, gourd) != 0
                || result.PermanentChanges.Count != 0) return false;
            result = Simulate([gourd with { Multicast = 1 }], [opening], 30);
            if (Snapshot(result, gourd).Slow != 20
                || result.States.Last(frame => frame.Tick.Value == 30 && frame.Player.Health > 0).Player.Health
                    != Math.Min(1000, 200 + healing[level - 1] * 2)) return false;
            var flying = Flying(gourd);
            if (Snapshot(Simulate([flying], [], 30), flying).Slow != 5) return false;
        }
        var source = Card(new WineGourdCardDefinition(), 1, 0);
        var bench = Idle(1) with { IsOnBench = true };
        var destroyed = DestroyAtStart(Idle(2));
        var enemy = Idle(0);
        var setup = Battle([source, bench, destroyed], [enemy], 65);
        var simulator = new CombatSimulator();
        var final = simulator.Simulate(setup);
        return final.Events.SequenceEqual(simulator.Simulate(setup).Events)
            && final.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == source.EntityId).Select(e => e.Tick.Value).SequenceEqual(new long[] { 30, 65 })
            && final.States.Where(frame => frame.Tick.Value == 40).All(frame => frame.Cards.Single(card => card.Id == source.EntityId).Slow == 0)
            && final.States.SelectMany(frame => frame.Cards).Where(card => card.Id != source.EntityId).All(card => card.Slow == 0);
    }

    internal static bool HatArmorAndCharge()
    {
        int[] armor = [40, 60, 80, 100];
        for (var level = 1; level <= 4; level++)
        {
            var hat = Card(new BambooHatCardDefinition(), level, 1);
            var alone = Simulate([hat], [], 140);
            if (alone.States.Last(frame => frame.Player.Health > 0).Player.Armor != armor[level - 1] * 2) return false;
            var slow = ActiveCard(0, 10, new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10));
            var result = Simulate([slow, hat], [Idle(0)], 40);
            if (EchoCount(result, hat) != 4 || result.Events.OfType<CardChargedEvent>().Count() != 4
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == hat.EntityId && !e.IsEcho) != 1
                || result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == hat.EntityId && !e.IsEcho).Tick.Value != 40
                || result.States.Last(frame => frame.Player.Health > 0).Player.Armor != armor[level - 1]) return false;
        }
        var receiver = Card(new BambooHatCardDefinition(), 1, 1);
        foreach (var effect in new EffectDefinition[]
        {
            new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 10),
            new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10),
            new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.SlowDuration, 10, GameTags.Human),
            new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10),
        })
        {
            var caster = ActiveCard(0, 20, effect);
            var result = Simulate([caster, receiver], [Idle(0)], 20);
            if (EchoCount(result, receiver) != 1 || result.Events.OfType<CardChargedEvent>().Single().AmountTicks != 10) return false;
        }
        var enemySlow = ActiveCard(0, 20, new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10));
        if (EchoCount(Simulate([receiver], [enemySlow], 20), receiver) != 0
            || EchoCount(Simulate([enemySlow, receiver], [], 20), receiver) != 0) return false;
        var zero = enemySlow with { Abilities = [Active(20, new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 1))] };
        if (EchoCount(Simulate([zero, receiver], [Flying(Idle(0))], 20), receiver) != 0) return false;
        var multiTarget = enemySlow with { Multicast = 1, Abilities = [Active(20,
            new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10, 2))] };
        if (EchoCount(Simulate([multiTarget, receiver], [Idle(0), Idle(1)], 20), receiver) != 4) return false;
        foreach (var inactive in new[] { receiver with { IsOnBench = true }, DestroyAtStart(receiver) })
            if (EchoCount(Simulate([enemySlow, inactive], [Idle(0)], 20), receiver) != 0) return false;
        var enemyReceiver = Card(new BambooHatCardDefinition(), 1, 1);
        return EchoCount(Simulate([], [enemySlow, enemyReceiver], 20), enemyReceiver) == 0
            && EchoCount(Simulate([Idle(0)], [enemySlow, enemyReceiver], 20), enemyReceiver) == 1;
    }

    internal static bool GroveFilteringAndEchoCutoff()
    {
        for (var level = 2; level <= 4; level++)
        {
            var grove = Card(new HiddenBambooGroveCardDefinition(), level, 1);
            var wood = ActiveCard(0, 20, new HealEffectDefinition(1)) with { ElementKeys = [GameElements.Wood], Multicast = 1 };
            var hat = Card(new BambooHatCardDefinition(), 1, 4);
            var enemy = Idle(0);
            var result = Simulate([wood, grove, hat], [enemy, Idle(1) with { IsOnBench = true }, DestroyAtStart(Idle(2))], 20);
            if (EchoCount(result, grove) != 2 || Snapshot(result, enemy).Slow != (level - 1) * 20
                || EchoCount(result, hat) != 0 || result.Events.OfType<CardChargedEvent>().Any()) return false;
            if (Snapshot(Simulate([wood, grove], [Flying(enemy)], 20), enemy).Slow != (level - 1) * 10) return false;
            foreach (var excluded in new[] { wood with { ElementKeys = [GameElements.General] }, wood with { IsOnBench = true },
                wood with { Abilities = [Active(20, new HealEffectDefinition(1), 1)] } })
                if (EchoCount(Simulate([excluded, grove], [enemy], 20), grove) != 0) return false;
            if (EchoCount(Simulate([grove], [wood], 20), grove) != 0) return false;
            foreach (var inactive in new[] { grove with { IsOnBench = true }, DestroyAtStart(grove) })
                if (EchoCount(Simulate([wood, inactive], [enemy], 20), grove) != 0) return false;
            if (Simulate([wood, grove], [], 20).Events.OfType<StatusChangedEvent>().Any()) return false;
        }
        // 酒葫芦发动同时造成己方迟缓、驱动木属性回响，斗笠只响应发动产生的迟缓。
        var gourd = Card(new WineGourdCardDefinition(), 1, 0);
        var bamboo = Card(new HiddenBambooGroveCardDefinition(), 2, 1);
        var receiver = Card(new BambooHatCardDefinition(), 1, 4);
        var combo = Simulate([gourd, bamboo, receiver], [Idle(0)], 30);
        return EchoCount(combo, bamboo) == 1 && EchoCount(combo, receiver) == 1;
    }

    internal static bool TempleTargetsAndDuration()
    {
        var temple = Card(new ZenLightTempleCardDefinition(), 4, 2);
        foreach (var effect in new EffectDefinition[]
        {
            new DamageEffectDefinition(0), new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
            new MaxHealthPercentDamageEffectDefinition(1), new SourceHeroLevelScaledDamageEffectDefinition(1),
            new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new SourceHeroArmorDamageEffectDefinition(),
        })
        {
            var attacker = ActiveCard(0, 20, effect) with { Multicast = 1 };
            foreach (var enemy in new[] { false, true })
            {
                var result = enemy ? Simulate([temple], [attacker], 20) : Simulate([attacker, temple], [], 20);
                if (EchoCount(result, temple) != 2 || Snapshot(result, attacker).Immobilize != 20
                    || Snapshot(result, temple).Immobilize != 0 || result.PermanentChanges.Count != 0) return false;
            }
        }
        var attack = ActiveCard(0, 20, new DamageEffectDefinition(1));
        var support = ActiveCard(1, 20, new HealEffectDefinition(1));
        var secondTemple = temple with { EntityId = EntityId.New(), BoardStart = 5 };
        var stacked = Simulate([attack, support, temple, secondTemple], [], 20);
        if (Snapshot(stacked, attack).Immobilize != 20 || Snapshot(stacked, support).Immobilize != 0) return false;
        if (Snapshot(Simulate([Flying(attack), temple], [], 20), attack).Immobilize != 5) return false;
        var duration = Simulate([attack, temple], [], 50);
        if (duration.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == attack.EntityId)
                .Select(e => e.Tick.Value).SequenceEqual(new long[] { 20, 50 }) == false
            || duration.States.Where(frame => frame.Tick.Value == 30).Any(frame => frame.Cards.Single(card => card.Id == attack.EntityId).Immobilize != 0)) return false;
        // 混合能力攻击卡牌的辅助发动也触发，失败发动、回响及战斗开始不触发。
        var mixed = attack with { Abilities = [Active(20, new DamageEffectDefinition(1)), Active(20, new HealEffectDefinition(1))] };
        if (EchoCount(Simulate([mixed, temple], [], 20), temple) != 2) return false;
        foreach (var excluded in new[] { support, attack with { IsOnBench = true },
            attack with { Abilities = [Active(20, new DamageEffectDefinition(1), 1)] },
            attack with { Abilities = [new AbilityDefinition("verification.jiyun.passive", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1)])] },
            attack with { Abilities = [new AbilityDefinition("verification.jiyun.echo", AbilityActivation.EchoOnAbilityActivated,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1)])] } })
            if (EchoCount(Simulate([excluded, temple], [support], 20), temple) != 0) return false;
        foreach (var inactive in new[] { temple with { IsOnBench = true }, DestroyAtStart(temple) })
            if (EchoCount(Simulate([attack, inactive], [], 20), temple) != 0) return false;
        var consumed = attack with { Abilities = [new AbilityDefinition("verification.jiyun.consume_attack", AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 20, [new DamageEffectDefinition(1), new DestroyCardEffectDefinition(false)])] };
        return EchoCount(Simulate([consumed, temple], [], 20), temple) == 0;
    }

    internal static bool ConfigurationBoundaries()
    {
        Action[] invalid =
        [
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 20,
                [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.Burn, 10)]),
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 20,
                [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, -1)]),
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
                [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10)]),
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.EchoOnAnyAttackCardActivated, AbilityTarget.EventCard, 0, 1,
                [new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 10)]),
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.EchoOnAlliedSlowApplied, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)], allowsBench: true),
            () => _ = new AbilityDefinition("verification.jiyun.invalid", AbilityActivation.EchoOnAnyAttackCardActivated, AbilityTarget.EventCard, 0, 0,
                [new DamageEffectDefinition(1)]),
        ];
        foreach (var action in invalid)
        {
            try { action(); return false; }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var instance = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(instance.Id, start, 0, 0, MatchSnapshot.CopyAbilities(instance.Abilities),
            UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size,
            ElementKeys: definition.Attributes.Identity.ElementKeys,
            ArmorAmount: instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor)) { Level = level };
    }
    private static AbilityDefinition Active(int ticks, EffectDefinition effect, int mana = 0) =>
        new("verification.jiyun.active", AbilityActivation.Active, AbilityTarget.EnemyHero, mana, ticks, [effect]);
    private static CardBattleSetup ActiveCard(int start, int ticks, EffectDefinition effect) =>
        new(EntityId.New(), start, 1, ticks, [Active(ticks, effect)], UseLegacyAttack: false);
    private static CardBattleSetup Idle(int start) => new(EntityId.New(), start, 0, 0, UseLegacyAttack: false);
    private static CardBattleSetup Flying(CardBattleSetup card) => card with
    {
        Abilities = (card.Abilities ?? []).Append(new AbilityDefinition("verification.jiyun.fly", AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)])).ToArray(),
    };
    private static CardBattleSetup DestroyAtStart(CardBattleSetup card) => card with
    {
        Abilities = (card.Abilities ?? []).Append(new AbilityDefinition("verification.jiyun.destroy", AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray(),
    };
    private static int EchoCount(BattleResult result, CardBattleSetup card) =>
        result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.EntityId && e.IsEcho);
    private static CardBattleSnapshot Snapshot(BattleResult result, CardBattleSetup card) => result.States[^1].Cards.Single(item => item.Id == card.EntityId);
    private static BattleResult Simulate(CardBattleSetup[] player, CardBattleSetup[] enemy, int ticks) => new CombatSimulator().Simulate(Battle(player, enemy, ticks));
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), enemy),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
