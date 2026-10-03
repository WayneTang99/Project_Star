using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 大教堂帕拉帝恩卡牌内容定义（内容层）。
public sealed class CathedralCardDefinition : CardDefinition
{
    public CathedralCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.cathedral"),
                    "大教堂",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light],
                    illustration: new StringName("res://art/ui/card-face/artwork/cathedral-illustration-refresh.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Aura, "己方战场区光属性卡牌获得1层多重；此卡牌被摧毁后失效，多张大教堂可叠加。"),
                    ]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 0,
                })),
            new TagSet([GameTags.Location]),
            initialLevel: 4,
            levels:
            [
                new CardLevelDefinition(
                    4,
                    null,
                    [
                        new AbilityDefinition(
                            new StringName("ability.light_multicast_aura"),
                            AbilityActivation.PassiveAura,
                            AbilityTarget.SelfCard,
                            0,
                            0,
                            [new GrantMulticastToAlliedElementCardsEffectDefinition(GameElements.Light, 1)]),
                    ]),
            ])
    {
    }
}
