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

    public static ShopOffer Create(CardDefinition definition, int? level = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var selectedLevel = level ?? definition.InitialLevel;
        if (!definition.SupportsLevel(selectedLevel))
            throw new ArgumentOutOfRangeException(nameof(level), $"Card does not provide level {selectedLevel}.");
        return new ShopOffer(
            definition,
            selectedLevel,
            CardValueCalculator.CalculateInitialValue(definition, selectedLevel));
    }
}
