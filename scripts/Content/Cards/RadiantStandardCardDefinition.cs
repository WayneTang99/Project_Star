using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 光辉旗帜圣骑士卡牌定义（内容层）。
public sealed class RadiantStandardCardDefinition : CardDefinition
{
    public RadiantStandardCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.radiant_standard"), "光辉旗帜", new StringName("paladin"),
                CardSize.Small, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/radiant_standard-illustration.png"), descriptionEntries:
                [new(CardKeywords.Aura, "己方存活战场光属性卡牌冷却缩减7/15%；多张百分比累加，最高100%，此卡牌被摧毁后失效。")])),
            new TagSet(), initialLevel: 3, levels: [CreateLevel(3, 7), CreateLevel(4, 15)]) { }

    private static CardLevelDefinition CreateLevel(int level, int percent) => new(level, null,
        [new AbilityDefinition(new StringName("ability.allied_element_cooldown_reduction_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0, [new ReduceAlliedElementCardCooldownAuraEffectDefinition(GameElements.Light, percent)])]);
}
