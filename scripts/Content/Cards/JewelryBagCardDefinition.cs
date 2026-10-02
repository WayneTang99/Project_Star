using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 珠宝袋无阵营卡牌内容定义（内容层）。
public sealed class JewelryBagCardDefinition : CardDefinition
{
    public JewelryBagCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.jewelry_bag"),
                    "珠宝袋",
                    GameFactions.Neutral,
                    CardSize.Small,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/jewelry_bag-illustration.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Sell, "当出售此卡牌后，随机获得一张与此卡牌同等级的材料卡；双棋盘已满时不获得。"),
                    ]),
                baseCombat: new ModifiableAttributeSet()),
            new TagSet(),
            initialLevel: 2,
            levels:
            [
                new CardLevelDefinition(2, null, System.Array.Empty<AbilityDefinition>()),
                new CardLevelDefinition(3, null, System.Array.Empty<AbilityDefinition>()),
                new CardLevelDefinition(4, null, System.Array.Empty<AbilityDefinition>()),
            ],
            onSellReward: new RandomTaggedCardOnSellDefinition(GameTags.Material))
    {
    }
}
