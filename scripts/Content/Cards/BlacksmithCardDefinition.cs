using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 铁匠铺帕拉帝恩卡牌内容定义（内容层）。
public sealed class BlacksmithCardDefinition : CardDefinition
{
    public BlacksmithCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.blacksmith"),
                    "铁匠铺",
                    new StringName("paladin"),
                    CardSize.Medium,
                    [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 60,
                })),
            new TagSet([GameTags.Location]),
            initialLevel: 2,
            levels:
            [
                CreateLevel(2, 10),
                CreateLevel(3, 20),
                CreateLevel(4, 40),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) =>
        new(
            level,
            null,
            [
                new AbilityDefinition(
                    new StringName("ability.improve_equipment"),
                    AbilityActivation.Active,
                    AbilityTarget.SelfCard,
                    0,
                    60,
                    [
                        new ModifyTaggedAlliedCardsAttributeEffectDefinition(
                            GameTags.Equipment,
                            GameAttributeKeys.AttackDamage,
                            amount),
                        new ModifyTaggedAlliedCardsAttributeEffectDefinition(
                            GameTags.Equipment,
                            GameAttributeKeys.Armor,
                            amount),
                    ]),
            ]);
}
