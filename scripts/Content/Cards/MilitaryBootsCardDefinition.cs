using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 军靴圣骑士卡牌内容定义（内容层）。
public sealed class MilitaryBootsCardDefinition : CardDefinition
{
    public MilitaryBootsCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.military_boots"),
                    "军靴",
                    new StringName("paladin"),
                    CardSize.Small,
                    [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 50,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 10),
                CreateLevel(2, 20),
                CreateLevel(3, 30),
                CreateLevel(4, 40),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int hasteDurationTicks) =>
        new(
            level,
            null,
            [
                new AbilityDefinition(
                    new StringName("ability.hasten_adjacent_allies"),
                    AbilityActivation.Active,
                    AbilityTarget.SelfCard,
                    0,
                    50,
                    [
                        new ApplyStatusToAdjacentAlliedCardsEffectDefinition(
                            BattleStatus.HasteDuration,
                            hasteDurationTicks,
                            GameTags.Human),
                    ]),
            ]);
}
