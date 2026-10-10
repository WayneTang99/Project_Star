using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 树根精粹无阵营材料卡牌内容定义（内容层）。
public sealed class RootEssenceCardDefinition : CardDefinition
{
    public RootEssenceCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.root_essence"), "树根精粹", GameFactions.Neutral,
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/root_essence-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌后，己方英雄永久增加1/2/3/4生命再生。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        Array.Empty<AbilityDefinition>(),
        onSellReward: new IncreaseHeroCombatAttributeOnSellDefinition(GameAttributeKeys.HealthRegen, level));
}
