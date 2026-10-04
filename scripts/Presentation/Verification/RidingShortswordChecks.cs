using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 骑佩短剑的相邻攻击发动筛选及回响生命周期验证（表现层验证模块）。
internal static class RidingShortswordChecks
{
    internal static bool LevelsAndAdjacency()
    {
        var definition = new RidingShortswordCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.riding_shortsword") || identity.DisplayName != "骑佩短剑"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Equipment) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Echo) return false;
        for (var level = 2; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var damage = 10 << (level - 2);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage
                || instance.Abilities.Any(ability => ability.Activation == AbilityActivation.Active
                    || ability.CooldownTicks != 0 || ability.ManaCost != 0)) return false;
            var sword = Sword(level) with { BoardStart = 3 };
            var left = Attacker(0, new DamageEffectDefinition(1)) with { OccupiedSlots = 3, Multicast = 1 };
            var right = Attacker(4, new DamageEffectDefinition(1)) with { OccupiedSlots = 2 };
            var setup = Battle([left, sword, right], [], 1000);
            var result = new CombatSimulator().Simulate(setup);
            var hits = SwordHits(result, sword);
            if (hits.Length != 3 || hits.Any(hit => hit.RawDamage != damage || hit.ArmorAbsorbed != damage)
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == sword.EntityId && item.IsEcho) != 3
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            if (SwordHits(new CombatSimulator().Simulate(Battle([sword], [])), sword).Length != 0) return false;
        }
        return true;
    }

    internal static bool AttackFilters()
    {
        var sword = Sword(2);
        // 全部直接伤害公式都属于攻击，零值攻击同样触发。
        EffectDefinition[] attacks =
        [
            new DamageEffectDefinition(0), new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
            new MaxHealthPercentDamageEffectDefinition(1), new SourceHeroLevelScaledDamageEffectDefinition(1),
            new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
            new SourceHeroArmorDamageEffectDefinition(),
        ];
        foreach (var effect in attacks)
            if (SwordHits(new CombatSimulator().Simulate(Battle([sword, Attacker(1, effect)], [])), sword).Length != 1)
                return false;
        EffectDefinition[] auxiliary =
        [new HealEffectDefinition(1), new GainSourceHeroArmorEffectDefinition(1),
            new ApplyStatusEffectDefinition(BattleStatus.Poison, 1), new ApplyStatusEffectDefinition(BattleStatus.Burn, 1)];
        foreach (var effect in auxiliary)
            if (SwordHits(new CombatSimulator().Simulate(Battle([sword, Attacker(1, effect)], [])), sword).Length != 0)
                return false;
        var human = Attacker(1, new DamageEffectDefinition(1));
        CardBattleSetup[] excluded =
        [human with { BoardStart = 2 }, human with { Tags = new TagSet([GameTags.Beast]) },
            human with { IsOnBench = true }, human with { Abilities = [Active(new DamageEffectDefinition(1), manaCost: 1)] }];
        foreach (var card in excluded)
            if (SwordHits(new CombatSimulator().Simulate(Battle([sword, card], [])), sword).Length != 0) return false;
        if (SwordHits(new CombatSimulator().Simulate(Battle([sword], [human])), sword).Length != 0) return false;
        // 按卡牌攻击能力分类：同一卡牌的辅助主动能力发动也计一次。
        var mixed = human with { Abilities = [Active(new DamageEffectDefinition(1)), Active(new HealEffectDefinition(1))] };
        return SwordHits(new CombatSimulator().Simulate(Battle([sword, mixed], [])), sword).Length == 2;
    }

    internal static bool EchoLifetime()
    {
        var sword = Sword(2) with { AttackDamage = 17 };
        var human = Attacker(1, new DamageEffectDefinition(1));
        var result = new CombatSimulator().Simulate(Battle([sword, human], []));
        if (SwordHits(result, sword).Single().RawDamage != 17) return false;
        var bench = sword with { IsOnBench = true };
        if (SwordHits(new CombatSimulator().Simulate(Battle([bench, human], [])), bench).Length != 0) return false;
        var destroy = new AbilityDefinition(new StringName("verification.shortsword.destroy"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
        var destroyed = sword with { Abilities = sword.Abilities!.Append(destroy).ToArray() };
        if (SwordHits(new CombatSimulator().Simulate(Battle([destroyed, human], [])), destroyed).Length != 0) return false;
        var second = Sword(2) with { BoardStart = 2, Tags = new TagSet([GameTags.Human]) };
        // 相邻人类回响攻击不再触发短剑，避免回响连锁。
        result = new CombatSimulator().Simulate(Battle([sword, human, second], []));
        if (SwordHits(result, sword).Length != 1 || SwordHits(result, second).Length != 1) return false;
        var passive = human with { Abilities = [new AbilityDefinition(new StringName("verification.shortsword.passive"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1)])] };
        if (SwordHits(new CombatSimulator().Simulate(Battle([sword, passive], [])), sword).Length != 0) return false;
        var skill = new SkillBattleSetup(EntityId.New(), passive.Abilities!, new System.Collections.Generic.Dictionary<StringName, int>());
        var skillSetup = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), [sword], [skill]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), []),
            42, new BattleTick(50), new BattleTick(51));
        if (SwordHits(new CombatSimulator().Simulate(skillSetup), sword).Length != 0) return false;
        try
        {
            _ = new AbilityDefinition(new StringName("verification.shortsword.invalid"),
                AbilityActivation.EchoOnAdjacentAlliedAttackCardActivated, AbilityTarget.EnemyHero, 0, 0,
                [new DamageEffectDefinition(1)], triggerCardTag: new StringName());
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    private static CardBattleSetup Sword(int level)
    {
        var configuration = new RidingShortswordCardDefinition().GetLevel(level)!;
        return new CardBattleSetup(EntityId.New(), 0, configuration.BaseCombatValues[GameAttributeKeys.AttackDamage],
            0, configuration.Abilities, UseLegacyAttack: false);
    }
    private static AbilityDefinition Active(EffectDefinition effect, int manaCost = 0) =>
        new(new StringName("verification.shortsword.active"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            manaCost, 50, [effect]);
    private static CardBattleSetup Attacker(int position, EffectDefinition effect) =>
        new(EntityId.New(), position, 1, 50, [Active(effect)], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]));
    private static DamageDealtEvent[] SwordHits(BattleResult result, CardBattleSetup sword) =>
        result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == sword.EntityId).ToArray();
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int armor = 0) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, armor, ManaRegen: 0), enemy),
            42, new BattleTick(50), new BattleTick(51));
}
