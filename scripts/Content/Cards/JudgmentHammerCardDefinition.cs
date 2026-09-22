using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 审判之锤卡牌内容定义（内容层）。
public sealed class JudgmentHammerCardDefinition : CardDefinition
{
    public JudgmentHammerCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.judgment_hammer"),
                    "审判之锤",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 60,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 3,
            levels:
            [
                CreateLevel(3, 60),
                CreateLevel(4, 50),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int cooldownTicks) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = cooldownTicks,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.judgment_hammer"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    cooldownTicks,
                    [new MaxHealthPercentDamageEffectDefinition(20)]),
            ]);
}
