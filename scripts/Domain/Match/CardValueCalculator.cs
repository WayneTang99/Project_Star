using System;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

public static class CardValueCalculator
{
    public static int CalculateInitialValue(CardDefinition definition, int level)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        if (definition.ValueCoefficient < 1)
        {
            throw new InvalidOperationException("Card value coefficient must be positive.");
        }

        return checked(definition.ValueCoefficient * level * definition.Attributes.Identity.OccupiedSlots);
    }

    public static int CalculateAcquiredValue(int initialValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialValue);
        return initialValue / 2;
    }
}
