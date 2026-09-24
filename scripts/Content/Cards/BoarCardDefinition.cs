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
                    [GameElements.General]),
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
