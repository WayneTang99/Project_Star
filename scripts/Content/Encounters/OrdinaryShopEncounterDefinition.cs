using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

public sealed class OrdinaryShopEncounterDefinition : CardShopEncounterDefinition
{
    public OrdinaryShopEncounterDefinition()
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            new StringName("encounter.shop.ordinary"), "普通商店",
            new StringName("res://art/ui/encounters/artwork/ordinary_shop-illustration-20261018.png"),
            "购买当前英雄职业的卡牌")), null, 1) { }
}
