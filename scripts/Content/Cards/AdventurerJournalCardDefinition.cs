using System;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冒险者日志罗宾卡牌定义，复用参战价值成长和出售经验奖励（内容层）。
public sealed class AdventurerJournalCardDefinition : CardDefinition
{
    public AdventurerJournalCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.adventurer_journal"), "冒险者日志", new StringName("robin"),
            CardSize.Small, [GameElements.General], setKey: new StringName("set.adventurer"),
            illustration: new StringName("res://art/ui/card-face/artwork/adventurer_journal-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.AfterBattle, "每场对战结束后，若此卡牌开战时位于战场区，其价值永久增加2/4/6/8，不论胜负。"),
                new(CardKeywords.Sell, "出售此卡牌时，己方英雄获得等同于此卡牌当前价值10%的经验，向下取整。"),
            ])), new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 6), CreateLevel(4, 8)],
            onSellReward: new GrantValuePercentExperienceOnSellDefinition(10)) { }

    private static CardLevelDefinition CreateLevel(int level, int valueBonus) => new(level, null,
        Array.Empty<AbilityDefinition>(), battleValueBonus: valueBonus);
}
