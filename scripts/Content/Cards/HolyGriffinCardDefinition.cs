using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 神圣狮鹫帕拉帝恩卡牌内容定义（内容层）。
public sealed class HolyGriffinCardDefinition : CardDefinition
{
    public HolyGriffinCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.holy_griffin"),
                    "神圣狮鹫",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 50,
                })),
            new TagSet([GameTags.Beast, GameTags.Mount]),
            initialLevel: 2,
            levels:
            [
                CreateLevel(2, 10),
                CreateLevel(3, 20),
                CreateLevel(4, 30),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int attackBonus) =>
        new(
            level,
            null,
            [
                new AbilityDefinition(
                    new StringName("ability.holy_griffin_hasten_adjacent_humans"),
                    AbilityActivation.Active,
                    AbilityTarget.SelfCard,
                    0,
                    50,
                    [
                        new ApplyStatusToAdjacentAlliedCardsEffectDefinition(
                            BattleStatus.HasteDuration,
                            10,
                            GameTags.Human,
                            1),
                    ]),
                new AbilityDefinition(
                    new StringName("ability.holy_griffin_empower_hastened_humans"),
                    AbilityActivation.PassiveAura,
                    AbilityTarget.OtherBattlefieldCards,
                    0,
                    0,
                    [
                        new ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition(
                            BattleStatus.HasteDuration,
                            GameTags.Human,
                            GameAttributeKeys.AttackDamage,
                            attackBonus),
                    ]),
            ]);
}
