using System;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

/// <summary>A snapshot of one card definition, level, and purchase price offered by a shop.</summary>
public sealed class ShopOffer
{
    private ShopOffer(CardDefinition definition, int level, int price)
    {
        Definition = definition;
        Level = level;
        Price = price;
    }

    public CardDefinition Definition { get; }

    public int Level { get; }

    public int Price { get; }

    public bool IsSold { get; private set; }

    internal void MarkSold() => IsSold = true;

    public static ShopOffer Create(CardDefinition definition, int level = 1)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ShopOffer(definition, level, CardValueCalculator.CalculateInitialValue(definition, level));
    }
}
