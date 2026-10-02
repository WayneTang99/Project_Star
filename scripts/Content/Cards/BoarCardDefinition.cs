using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 野猪无阵营卡牌内容定义（内容层）。
public sealed class BoarCardDefinition : CardDefinition
{
    public BoarCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.boar"),
                    "野猪",
                    GameFactions.Neutral,
                    CardSize.Medium,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/boar-illustration.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却6秒，对敌方英雄造成20/30/50/100 × 己方英雄当前生命比例的普通伤害（小数向下取整，先扣护甲）。"),
                    ]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 20,
                    [GameAttributeKeys.CooldownTicks] = 60,
                })),
            new TagSet([GameTags.Beast]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 20),
                CreateLevel(2, 30),
                CreateLevel(3, 50),
                CreateLevel(4, 100),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int damage) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = damage,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.source_hero_health_scaled_damage"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    60,
                    [new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            ]);
}
