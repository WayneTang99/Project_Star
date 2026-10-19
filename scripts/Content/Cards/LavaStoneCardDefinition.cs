using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 熔岩石无阵营材料卡牌定义，复用出售灼伤成长（内容层）。
public sealed class LavaStoneCardDefinition : CardDefinition
{
    public LavaStoneCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.lava_stone"), "熔岩石", new StringName("neutral"),
                CardSize.Small, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/lava_stone-illustration-20261019.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌时，己方战场最左侧灼伤卡牌施加的灼伤永久增加1/2/3/4。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        Array.Empty<AbilityDefinition>(), onSellReward: new IncreaseLeftmostBurnCardOnSellDefinition(level));
}
