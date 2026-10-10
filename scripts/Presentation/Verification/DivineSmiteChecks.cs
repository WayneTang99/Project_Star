using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Content.Heroes;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 至圣斩的筛选、动态倍率、导师等级与冻结结果验证（表现层验证模块）。
internal static class DivineSmiteChecks
{
    internal static bool DefinitionAndMentor()
    {
        var catalog = DefinitionRegistry.Scan(typeof(DivineSmiteChecks).Assembly);
        var definition = catalog.Skills["skill.divine_smite"];
        if (definition is not DivineSmiteSkillDefinition || definition.InitialLevel != 4
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || !definition.SupportsLevel(4) || Enumerable.Range(1, 5).Where(level => level != 4).Any(definition.SupportsLevel))
            return false;
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(1, 0, new PaladinHeroDefinition());
        var service = new MentorService(catalog, new SkillAcquisitionService(factory));
        var low = service.Open(session, "mentor.archbishop").Value!;
        if (low.Offers.Any(offer => offer.SkillKey == definition.Attributes.Identity.Key)) return false;
        if (service.ChooseSkill(session, low, low.Offers[0].SkillKey).IsFailure) return false;
        var high = service.Open(session, "mentor.archbishop", 4).Value!;
        if (!high.Offers.Any(offer => offer.SkillKey == definition.Attributes.Identity.Key && offer.Level == 4)
            || service.ChooseSkill(session, high, definition.Attributes.Identity.Key).Value?.CurrentLevel != 4) return false;
        var details = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, 4));
        return details.Contains("光属性卡牌攻击 × 2") && details.Contains("恶魔或亡灵") && details.Contains("光环");
    }

    internal static bool FilteringAndDamage()
    {
        // 无目标、任一标签、双标签、多张目标、备战目标与开场已摧毁目标。
        CardBattleSetup[][] enemies = [[], [Tagged(GameTags.Demon)], [Tagged(GameTags.Undead)],
            [Tagged(GameTags.Demon, GameTags.Undead)], [Tagged(GameTags.Demon), Tagged(GameTags.Undead) with { BoardStart = 1 }],
            [Tagged(GameTags.Demon) with { IsOnBench = true }],
            [Tagged(GameTags.Undead) with { Abilities = [Destroy(0)], UseLegacyAttack = false }]];
        int[] factors = [2, 4, 4, 4, 4, 2, 2];
        for (var index = 0; index < enemies.Length; index++)
        {
            var light = Attack(0, GameElements.Light);
            var dual = Attack(1, GameElements.Light, GameElements.Fire);
            var other = Attack(2, GameElements.Fire);
            var bench = Attack(0, GameElements.Light) with { IsOnBench = true };
            var alliedDemon = Tagged(GameTags.Demon) with { BoardStart = 3 };
            var enemyAttack = Attack(2, GameElements.Light);
            var setup = Battle([light, dual, other, bench, alliedDemon], enemies[index].Append(enemyAttack).ToArray(), 2);
            var result = new CombatSimulator().Simulate(setup);
            foreach (var card in new[] { light, dual })
            {
                var hit = result.Events.OfType<DamageDealtEvent>().Single(value => value.SourceCardId == card.EntityId);
                if (hit.RawDamage != 10 * factors[index]
                    || result.States.Last().Cards.Single(value => value.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage] != hit.RawDamage)
                    return false;
            }
            if (result.Events.OfType<DamageDealtEvent>().Single(value => value.SourceCardId == other.EntityId).RawDamage != 10
                || result.Events.OfType<DamageDealtEvent>().Single(value => value.SourceCardId == enemyAttack.EntityId).RawDamage != 10
                || result.Events.OfType<DamageDealtEvent>().Any(value => value.SourceCardId == bench.EntityId)
                || result.States.Last().Cards.Single(value => value.Id == bench.EntityId).Values[GameAttributeKeys.AttackDamage] != 10
                || light.AttackDamage != 10 || result.PermanentChanges.Count != 0
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        return true;
    }

    internal static bool DynamicConditions()
    {
        var attacker = Attack(0, GameElements.Light);
        var demon = Tagged(GameTags.Demon) with { Abilities = [Destroy(3)], UseLegacyAttack = false };
        var result = new CombatSimulator().Simulate(Battle([attacker], [demon], 4));
        if (!Hits(result, attacker.EntityId).SequenceEqual(new[] { 40, 20 })) return false;
        // 临时恶魔标签来源失效后恢复；敌方卡牌本身仍存活。
        var grant = new CardBattleSetup(EntityId.New(), 1, 0, 3,
            [new AbilityDefinition("ability.verification.demon_aura", AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0, [new GrantTagToEnemySizeCardsEffectDefinition(CardSize.Small, GameTags.Demon)]), Destroy(3)],
            UseLegacyAttack: false);
        result = new CombatSimulator().Simulate(Battle([attacker, grant], [Tagged()], 4));
        if (!Hits(result, attacker.EntityId).SequenceEqual(new[] { 40, 20 })) return false;
        // 敌方标签通过转变移除，当场恢复基础倍率。
        var transform = new CardBattleSetup(EntityId.New(), 0, 0, 1,
            [new AbilityDefinition("ability.verification.remove_demon", AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 1, [new TransformRandomEnemyCardEffectDefinition(new TestCardDefinition(GameElements.Fire))])],
            UseLegacyAttack: false);
        result = new CombatSimulator().Simulate(Battle([transform, attacker with { BoardStart = 1 }], [Tagged(GameTags.Demon)], 2));
        if (!Hits(result, attacker.EntityId).SequenceEqual(new[] { 20 })) return false;
        // 新召唤光属性攻击卡牌立即参与光环，敌方新召唤亡灵立即满足条件。
        var alliedSummoner = Summoner(new TestCardDefinition(GameElements.Light), 0);
        var enemySummoner = Summoner(new TestCardDefinition(GameElements.Fire, GameTags.Undead), 0);
        result = new CombatSimulator().Simulate(Battle([alliedSummoner], [enemySummoner], 2));
        var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
        var alliedId = summons.Single(value => value.Side == SideId.Player).SummonedCardId;
        return Hits(result, alliedId).SequenceEqual(new[] { 40 });
    }

    internal static bool StackingAndValidation()
    {
        var attacker = Attack(0, GameElements.Light);
        var additive = new CardBattleSetup(EntityId.New(), 1, 0, 1,
            [new AbilityDefinition("ability.verification.additive", AbilityActivation.PassiveAura, AbilityTarget.SelfCard,
                0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 5)])], UseLegacyAttack: false);
        var single = new CombatSimulator().Simulate(Battle([attacker, additive], [Tagged(GameTags.Demon)], 2));
        if (!Hits(single, attacker.EntityId).SequenceEqual(new[] { 60 })) return false;
        var stacked = new CombatSimulator().Simulate(Battle([attacker, additive], [Tagged(GameTags.Demon)], 2, 2));
        if (!Hits(stacked, attacker.EntityId).SequenceEqual(new[] { 240 })) return false;
        var tags = new List<StringName> { GameTags.Demon };
        var aura = new AbilityDefinition("ability.verification.frozen", AbilityActivation.PassiveAura, AbilityTarget.AlliedHero,
            0, 0, [new MultiplyAlliedElementCardAttributeAuraEffectDefinition(GameElements.Light, GameAttributeKeys.AttackDamage, 2, tags)]);
        tags[0] = GameTags.Undead;
        if (((MultiplyAlliedElementCardAttributeAuraEffectDefinition)aura.Effects[0]).RequiredEnemyAnyTags![0] != GameTags.Demon)
            return false;
        try
        {
            _ = new AbilityDefinition("ability.verification.invalid", AbilityActivation.PassiveAura, AbilityTarget.AlliedHero,
                0, 0, [new MultiplyAlliedElementCardAttributeAuraEffectDefinition(GameElements.Light, GameAttributeKeys.AttackDamage, 0)]);
            return false;
        }
        catch (ArgumentException) { }
        return stacked.PermanentChanges.Count == 0 && attacker.AttackDamage == 10;
    }

    private static int[] Hits(BattleResult result, EntityId id) => result.Events.OfType<DamageDealtEvent>()
        .Where(value => value.SourceCardId == id).Select(value => value.RawDamage).ToArray();

    private static CardBattleSetup Attack(int position, params StringName[] elements) => new(EntityId.New(), position, 10, 2,
        [new AbilityDefinition("ability.verification.attack", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 2,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], ElementKeys: elements);

    private static CardBattleSetup Tagged(params StringName[] tags) => new(EntityId.New(), 0, 0, 1,
        UseLegacyAttack: false, Tags: new TagSet(tags));

    private static AbilityDefinition Destroy(int tick) => new("ability.verification.destroy", tick == 0
        ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.Active, AbilityTarget.SelfCard, 0, tick,
        [new DestroyCardEffectDefinition(false)]);

    private static CardBattleSetup Summoner(CardDefinition card, int position) => new(EntityId.New(), position, 0, 1,
        [new AbilityDefinition("ability.verification.summon", AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard,
            0, 0, [new SummonAdjacentCardEffectDefinition(card, AdjacentCardSide.Right)])], UseLegacyAttack: false) { Level = 4 };

    private static BattleSetup Battle(IReadOnlyList<CardBattleSetup> allies, IReadOnlyList<CardBattleSetup> enemies, int timeout, int count = 1)
    {
        var factory = new EntityFactory();
        var skills = Enumerable.Range(0, count).Select(_ => factory.CreateSkill(new DivineSmiteSkillDefinition()))
            .Select(skill => new SkillBattleSetup(skill.Id, skill.Abilities, skill.Attributes.BaseCombat.SnapshotFinalValues())).ToArray();
        return new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), allies, skills),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), enemies), 7, new BattleTick(timeout), new BattleTick(300));
    }

    // 仅在验证中提供可召唤/转变的最小攻击模板，不进入正式池。
    private sealed class TestCardDefinition : CardDefinition
    {
        internal TestCardDefinition(StringName element, params StringName[] tags)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes("card.verification.smite", "验证卡牌",
                GameFactions.Neutral, CardSize.Small, [element]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 10 })),
                new TagSet(tags), abilities: [new AbilityDefinition("ability.verification.attack", AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 2, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], initialLevel: 4)
        {
        }
    }
}
