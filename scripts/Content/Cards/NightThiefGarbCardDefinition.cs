using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 夜盗行衣瓦洛斯卡牌内容定义（内容层）。
public sealed class NightThiefGarbCardDefinition : CardDefinition
{
    public NightThiefGarbCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.night_thief_garb"), "夜盗行衣", new StringName("valos"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/night_thief_garb-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却4秒，魔法消耗0，随机一张己方存活战场卡牌获得1/2/3/4秒疾速，可选中自身。"),
                 new(CardKeywords.RoundStart, "轮初，随机获得一张当前英雄以外的同级小型卡牌（含无阵营）；仅在战场区生效。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hasten_random_ally"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 40,
            [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, level * 10)]),
         new AbilityDefinition(new StringName("ability.round_random_other_hero_card"), AbilityActivation.PassiveOnRoundStart,
            AbilityTarget.AlliedHero, 0, 0,
            [new GrantRandomOtherHeroSourceLevelCardEffectDefinition(CardSize.Small)])]);
}
