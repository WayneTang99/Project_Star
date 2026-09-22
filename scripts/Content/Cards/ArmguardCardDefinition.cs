using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 臂铠圣骑士卡牌内容定义（内容层）。
public sealed class ArmguardCardDefinition : CardDefinition
{
    public ArmguardCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.armguard"),
                    "臂铠",
                    new StringName("paladin"),
                    CardSize.Small,
                    [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 5,
                    [GameAttributeKeys.CooldownTicks] = 50,
                    [GameAttributeKeys.Multicast] = 1,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 5),
                CreateLevel(2, 10),
                CreateLevel(3, 15),
                CreateLevel(4, 20),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = amount,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.armguard"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    50,
                    [
                        new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                        new GainSourceHeroArmorEffectDefinition(amount),
                    ]),
            ]);
}
