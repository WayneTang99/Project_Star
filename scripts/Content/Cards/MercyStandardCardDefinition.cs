using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 怜悯旗帜圣骑士卡牌定义（内容层）。
public sealed class MercyStandardCardDefinition : CardDefinition
{
    public MercyStandardCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.mercy_standard"), "怜悯旗帜", new StringName("paladin"),
                CardSize.Small, [GameElements.General], descriptionEntries:
                [new(CardKeywords.Aura, "己方存活战场卡牌治疗+20/40/80，包含自身；多张可叠加，此卡牌被摧毁后失效。")])),
            new TagSet(), initialLevel: 2, levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.allied_card_healing_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.HealingBonus, amount)])]);
}
