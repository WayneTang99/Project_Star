using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 百宝箱无阵营卡牌内容定义（内容层）。
public sealed class TreasureChestCardDefinition : CardDefinition
{
    public TreasureChestCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.treasure_chest"),
                    "百宝箱",
                    GameFactions.Neutral,
                    CardSize.Medium,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/treasure_chest-illustration.png"),
                    description: "回响：当出售此卡牌后，获得3件与此卡牌同等级的随机小型材料；每件奖励独立结算自动放置与合并，无法创建时跳过该件。"),
                baseCombat: new ModifiableAttributeSet()),
            new TagSet(),
            initialLevel: 3,
            levels:
            [
                new CardLevelDefinition(3, null, System.Array.Empty<AbilityDefinition>()),
                new CardLevelDefinition(4, null, System.Array.Empty<AbilityDefinition>()),
            ],
            onSellReward: new RandomTaggedCardOnSellDefinition(
                GameTags.Material,
                Count: 3,
                RequiredSize: CardSize.Small))
    {
    }
}
