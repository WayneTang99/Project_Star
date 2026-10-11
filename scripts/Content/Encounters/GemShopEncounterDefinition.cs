using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 宝石不足三种时只生成实际可用的砧板。
public sealed class GemShopEncounterDefinition : GemShopDefinition
{
    public GemShopEncounterDefinition() : base(new EntityAttributes<EncounterIdentityAttributes>(
        new EncounterIdentityAttributes(new StringName("encounter.shop.gems"), "宝石商店",
            new StringName("res://art/ui/encounters/artwork/gem_shop-illustration.png"),
            "拖动发动卡牌到砧板，选择一颗宝石镶嵌 · 20金币")), 4, 3, 20) { }
}
