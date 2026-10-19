using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 黑市典当铺瓦洛斯卡牌内容定义（内容层）。
public sealed class BlackMarketPawnshopCardDefinition : CardDefinition
{
    public BlackMarketPawnshopCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.black_market_pawnshop"), "黑市典当铺", new StringName("valos"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/black_market_pawnshop-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Aura, "己方攻击卡牌获得等同于此卡牌价值的攻击；仅在战场区生效。"),
                 new(CardKeywords.Trade, "在战场区或备战区时，出售其他卡牌后，此卡牌获得等同于该卡牌价值×10/15/20/30%+1的价值（百分比向下取整）。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Building]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 15), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int percent) => new(level, null,
        [new AbilityDefinition(new StringName("ability.source_value_attack_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 0,
                SourceValueMultiplier: 1, AttackCardsOnly: true)])],
        saleAttributeBonuses: [new TaggedCardSaleAttributeBonus(new StringName(""), GameAttributeKeys.Value, 1, percent)]);
}
