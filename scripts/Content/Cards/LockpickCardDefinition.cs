using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 开锁器瓦洛斯卡牌内容定义（内容层）。
public sealed class LockpickCardDefinition : CardDefinition
{
    public LockpickCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.lockpick"), "开锁器", new StringName("valos"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/lockpick-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Sell, "出售此卡牌时，己方战场最左侧疾速卡牌每次施加的疾速时长永久增加0.5/1/1.5秒。")]),
            baseCombat: new ModifiableAttributeSet()),
            new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2, 5), CreateLevel(3, 10), CreateLevel(4, 15)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amountTicks) => new(level, null,
        Array.Empty<AbilityDefinition>(), onSellReward: new IncreaseLeftmostHasteCardOnSellDefinition(amountTicks));
}
