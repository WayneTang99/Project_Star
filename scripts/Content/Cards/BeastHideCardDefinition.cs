using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 兽皮无阵营卡牌内容定义（内容层）。
public sealed class BeastHideCardDefinition : CardDefinition
{
    public BeastHideCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.beast_hide"),
                    "兽皮",
                    GameFactions.Neutral,
                    CardSize.Small,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/beast_hide-illustration.png"),
                    description: "回响：当获得此卡牌后，此卡牌价值增加2/4/8/16。"),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 1,
                })),
            new TagSet([GameTags.Material]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 2),
                CreateLevel(2, 4),
                CreateLevel(3, 8),
                CreateLevel(4, 16),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int acquiredValueBonus) =>
        new(
            level,
            null,
            System.Array.Empty<AbilityDefinition>(),
            acquiredValueBonus: acquiredValueBonus);
}
