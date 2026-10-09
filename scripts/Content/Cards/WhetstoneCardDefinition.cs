using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 磨刀石无阵营卡牌内容定义（内容层）。
public sealed class WhetstoneCardDefinition : CardDefinition
{
    public WhetstoneCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.whetstone"), "磨刀石", new StringName("neutral"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/whetstone-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌时，己方战场最左侧攻击卡牌永久增加5/10/15/20攻击。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 5), CreateLevel(2, 10), CreateLevel(3, 15), CreateLevel(4, 20)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        Array.Empty<AbilityDefinition>(), onSellReward: new IncreaseLeftmostAttackCardOnSellDefinition(amount));
}
