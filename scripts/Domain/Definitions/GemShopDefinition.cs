using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 宝石渠道的等级、砧板上限与镶嵌价格，不限定具体宝石种类。
public abstract class GemShopDefinition : ShopEncounterDefinition
{
    protected GemShopDefinition(EntityAttributes<EncounterIdentityAttributes> attributes, int level, int offerCount, int socketCost)
        : base(attributes, level)
    {
        if (level is < 1 or > 5 || offerCount < 1 || socketCost < 0) throw new ArgumentOutOfRangeException(nameof(level));
        OfferCount = offerCount; SocketCost = socketCost;
    }
    public int OfferCount { get; }
    public int SocketCost { get; }
}
