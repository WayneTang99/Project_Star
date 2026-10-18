using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

public sealed class LightShopEncounterDefinition : CardShopEncounterDefinition
{
    public LightShopEncounterDefinition()
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            new StringName("encounter.shop.light"), "光属性商店",
            new StringName("res://art/ui/encounters/artwork/light_shop-illustration-20261018.png"),
            "购买任意英雄的光属性卡牌", allowedHeroKeys: [new StringName("hero.paladin")])),
            null, 3, onlyHeroFaction: false, requiredElement: GameElements.Light) { }
}
