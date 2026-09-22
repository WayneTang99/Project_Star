using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔能盾圣骑士卡牌内容定义（内容层）。
public sealed class ArcaneShieldCardDefinition : CardDefinition
{
    public ArcaneShieldCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.arcane_shield"),
                    "魔能盾",
                    new StringName("paladin"),
                    CardSize.Medium,
                    [GameElements.Light]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 80,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 2,
            levels:
            [
                CreateLevel(2, 80, 20),
                CreateLevel(3, 70, 40),
                CreateLevel(4, 60, 80),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int cooldownTicks, int manaCost) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = cooldownTicks,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.gain_armor_from_mana_spent"),
                    AbilityActivation.Active,
                    AbilityTarget.AlliedHero,
                    manaCost,
                    cooldownTicks,
                    [new GainArmorEqualToManaSpentEffectDefinition()]),
            ]);
}
