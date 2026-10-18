using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 商品池通过尺寸、归属、标签与元素的通用条件筛选。
public abstract class CardShopEncounterDefinition : ShopEncounterDefinition
{
    protected CardShopEncounterDefinition(EntityAttributes<EncounterIdentityAttributes> attributes,
        CardSize? cardSize, int level, int minimumRound = 1, int maximumRound = 99, int baseWeight = 1,
        bool onlyHeroFaction = true, Godot.StringName? requiredTag = null, Godot.StringName? requiredElement = null)
        : base(attributes, level, minimumRound, maximumRound, baseWeight)
    {
        if (cardSize is { } size && !Enum.IsDefined(size)) throw new ArgumentOutOfRangeException(nameof(cardSize));
        if (requiredTag is { IsEmpty: true }) throw new ArgumentException("Required tag cannot be empty.", nameof(requiredTag));
        if (requiredElement is { IsEmpty: true } || requiredElement is not null && !GameElements.IsValid(requiredElement))
            throw new ArgumentException("Required element must be a registered element.", nameof(requiredElement));
        RequiredElement = requiredElement;
        RequiredSize = cardSize; OnlyHeroFaction = onlyHeroFaction; RequiredTag = requiredTag;
    }

    public CardSize? RequiredSize { get; }
    public bool OnlyHeroFaction { get; }
    public Godot.StringName? RequiredTag { get; }
    public Godot.StringName? RequiredElement { get; }
}
