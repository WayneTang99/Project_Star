using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 浆果无阵营卡牌内容定义（内容层）。
public sealed class BerriesCardDefinition : CardDefinition
{
    public BerriesCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.berries"), "浆果", GameFactions.Neutral,
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/berries-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌后，己方英雄永久增加5/10/15/20最大生命值。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Plant]), initialLevel: 1,
            levels: [CreateLevel(1, 5), CreateLevel(2, 10), CreateLevel(3, 15), CreateLevel(4, 20)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        Array.Empty<AbilityDefinition>(),
        onSellReward: new IncreaseHeroCombatAttributeOnSellDefinition(GameAttributeKeys.MaxHealth, amount));
}
