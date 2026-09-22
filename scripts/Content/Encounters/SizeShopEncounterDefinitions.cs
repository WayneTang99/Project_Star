using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 只提供当前英雄小型卡牌的1级商店（内容层）。
public sealed class SmallShopEncounterDefinition : ShopEncounterDefinition
{
    public SmallShopEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.shop.small"), "小型商店")),
            CardSize.Small,
            1)
    {
    }
}

// 只提供当前英雄中型卡牌的1级商店（内容层）。
public sealed class MediumShopEncounterDefinition : ShopEncounterDefinition
{
    public MediumShopEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.shop.medium"), "中型商店")),
            CardSize.Medium,
            1)
    {
    }
}

// 只提供当前英雄大型卡牌的2级商店（内容层）。
public sealed class LargeShopEncounterDefinition : ShopEncounterDefinition
{
    public LargeShopEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.shop.large"), "大型商店")),
            CardSize.Large,
            2)
    {
    }
}
