using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 军靴帕拉帝恩卡牌内容定义（内容层）。
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
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/military_boots-illustration-refresh.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却5秒，魔法消耗0，使左右直接相邻的己方战场卡牌获得1/2/3/4秒疾速；相邻卡牌具有人类标签时，持续时间翻倍。"),
                    ]),
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
