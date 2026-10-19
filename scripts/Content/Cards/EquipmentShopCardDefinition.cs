using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 军械库帕拉帝恩建筑卡牌，组合价值攻防光环与装备拾取成长（内容层）。
public sealed class EquipmentShopCardDefinition : CardDefinition
{
    public EquipmentShopCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.equipment_shop"), "军械库", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/armory-illustration-20261019.png"),
                descriptionEntries:
                [new(CardKeywords.Aura, "己方战场装备卡牌的攻击和护甲增加等同于此卡牌当前价值的数值；仅增强已有对应能力，来源仅在战场区生效。"),
                 new(CardKeywords.Trade, "在战场区或备战区时，每成功获得1张装备卡牌后，此卡牌价值永久增加1/2/3；合并获得也触发。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Building]), initialLevel: 2,
            levels: [CreateLevel(2, 1), CreateLevel(3, 2), CreateLevel(4, 3)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.source_value_equipment_attribute_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 0,
                SourceValueMultiplier: 1, RequiredTag: GameTags.Equipment),
             new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.Armor, 0,
                SourceValueMultiplier: 1, RequiredTag: GameTags.Equipment)])],
        acquisitionAttributeBonuses: [new TaggedCardAcquisitionAttributeBonus(GameTags.Equipment, GameAttributeKeys.Value, amount)]);
}
