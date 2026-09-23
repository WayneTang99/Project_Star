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
                    [GameElements.General]),
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
