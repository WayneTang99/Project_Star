using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 按型号限制商品池的商店遭遇定义（领域定义层）。
public abstract class ShopEncounterDefinition : EncounterDefinition
{
    protected ShopEncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        CardSize cardSize,
        int level,
        int minimumRound = 1,
        int maximumRound = 99,
        int baseWeight = 1)
        : base(attributes, minimumRound, maximumRound, EncounterKind.Shop, baseWeight)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        CardSize = cardSize;
        Level = level;
    }

    public CardSize CardSize { get; }

    public int Level { get; }
}
