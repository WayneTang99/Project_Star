using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 按卡牌尺寸限制商品池的商店子类。
public abstract class CardShopEncounterDefinition : ShopEncounterDefinition
{
    protected CardShopEncounterDefinition(EntityAttributes<EncounterIdentityAttributes> attributes,
        CardSize cardSize, int level, int minimumRound = 1, int maximumRound = 99, int baseWeight = 1)
        : base(attributes, level, minimumRound, maximumRound, baseWeight)
    {
        if (!Enum.IsDefined(cardSize)) throw new ArgumentOutOfRangeException(nameof(cardSize));
        CardSize = cardSize;
    }

    public CardSize CardSize { get; }
}
