using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 银针草莫娜卡牌内容定义（内容层）。
public sealed class SilverNeedleGrassCardDefinition : CardDefinition
{
    public SilverNeedleGrassCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.silver_needle_grass"), "银针草", new StringName("mona"),
                CardSize.Small, [GameElements.Wood],
                illustration: new StringName("res://art/ui/card-face/artwork/silver_needle_grass-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌时，己方战场最左侧治疗卡牌永久增加5/10/15/20治疗。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Plant, GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 5), CreateLevel(2, 10), CreateLevel(3, 15), CreateLevel(4, 20)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        Array.Empty<AbilityDefinition>(), onSellReward: new IncreaseLeftmostHealingCardOnSellDefinition(amount));
}
