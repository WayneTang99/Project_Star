using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 修女帕拉帝恩卡牌内容定义（内容层）。
public sealed class NunCardDefinition : CardDefinition
{
    public NunCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.nun"),
                    "修女",
                    new StringName("paladin"),
                    CardSize.Small,
                    [GameElements.Light]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 60,
                })),
            new TagSet([GameTags.Human]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 10),
                CreateLevel(2, 20),
                CreateLevel(3, 40),
                CreateLevel(4, 80),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int healing) =>
        new(
            level,
            null,
            [
                new AbilityDefinition(
                    new StringName("ability.nun_prayer"),
                    AbilityActivation.Active,
                    AbilityTarget.AlliedHero,
                    10,
                    60,
                    [
                        new HealEffectDefinition(healing),
                        new ChargeRandomOtherAlliedElementCardEffectDefinition(GameElements.Light, 10),
                    ]),
            ]);
}
