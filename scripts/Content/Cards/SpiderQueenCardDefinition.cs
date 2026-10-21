using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 蜘蛛女皇莫娜卡牌内容定义（内容层）。
public sealed class SpiderQueenCardDefinition : CardDefinition
{
    public SpiderQueenCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.spider_queen"), "蜘蛛女皇", new StringName("mona"), CardSize.Large, [GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/spider_queen-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "在己方战场所有空位召唤【毒新妇】。"),
             new(CardKeywords.Aura, "己方小型虫族卡牌冷却减少1秒并获得消耗。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 90 })),
            new TagSet([GameTags.Insect]), initialLevel: 3, levels: [CreateLevel(3, 90), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.summon_all_empty_slots"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, cooldown, [new SummonIntoAllEmptySlotsEffectDefinition(new VenomBrideCardDefinition())]),
         new AbilityDefinition(new StringName("ability.small_insect_cooldown_and_consume_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0, [new ReduceAlliedTaggedCardCooldownAuraEffectDefinition(GameTags.Insect, CardSize.Small, 10),
                new GrantConsumeToAlliedTaggedCardsAuraEffectDefinition(GameTags.Insect, CardSize.Small)])]);
}
