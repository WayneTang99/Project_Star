using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 钻石无阵营材料卡牌内容定义（内容层）。
public sealed class DiamondCardDefinition : CardDefinition
{
    public DiamondCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.diamond"),
                    "钻石",
                    GameFactions.Neutral,
                    CardSize.Small,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/diamond-illustration-refresh.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Pickup, "当获得此卡牌后，此卡牌价值增加20。"),
                    ]),
                baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Material]),
            initialLevel: 4,
            levels:
            [
                new CardLevelDefinition(
                    4,
                    null,
                    System.Array.Empty<AbilityDefinition>(),
                    acquiredValueBonus: 20),
            ])
    {
    }
}
