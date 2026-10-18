using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 跨职业、全尺寸的装备标签商店；等级与刷新遵循通用商店规则。
public sealed class EquipmentShopEncounterDefinition : CardShopEncounterDefinition
{
    public EquipmentShopEncounterDefinition()
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            new StringName("encounter.shop.equipment"), "装备商店",
            new StringName("res://art/ui/card-face/artwork/equipment_shop-illustration.png"),
            "购买任意职业的装备卡牌", allowedHeroKeys: [new StringName("hero.paladin"), new StringName("hero.harla")])), null, 1, onlyHeroFaction: false, requiredTag: GameTags.Equipment)
    {
    }
}
