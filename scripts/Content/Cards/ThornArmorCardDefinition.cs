using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 荆棘甲帕拉帝恩卡牌内容定义（内容层）。
public sealed class ThornArmorCardDefinition : CardDefinition
{
    public ThornArmorCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.thorn_armor"),
                    "荆棘甲",
                    new StringName("paladin"),
                    CardSize.Medium,
                    [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.Armor] = 10,
                    [GameAttributeKeys.CooldownTicks] = 80,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 80, 10),
                CreateLevel(2, 70, 20),
                CreateLevel(3, 60, 40),
                CreateLevel(4, 50, 80),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int cooldownTicks, int armorAmount) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.Armor] = armorAmount,
                [GameAttributeKeys.CooldownTicks] = cooldownTicks,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.thorn_armor"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    cooldownTicks,
                    [
                        new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
                        new SourceHeroArmorDamageEffectDefinition(),
                    ]),
            ]);
}
