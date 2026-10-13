using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 卡牌与宝石商店共用的遭遇排程及等级（领域定义层）。
public abstract class ShopEncounterDefinition : EncounterDefinition
{
    protected ShopEncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        int level,
        int minimumRound = 1,
        int maximumRound = 99,
        int baseWeight = 1)
        : base(attributes, minimumRound, maximumRound, EncounterKind.Shop, baseWeight)
    {
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
    }

    public int Level { get; }
}
