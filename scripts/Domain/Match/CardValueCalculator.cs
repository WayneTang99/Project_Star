using System;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

public static class CardValueCalculator
{
    public static int CalculateInitialValue(CardDefinition definition, int level)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.SupportsLevel(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), $"Card does not provide level {level}.");
        }

        var configuredValue = definition.GetLevel(level)?.InitialValue;
        if (configuredValue.HasValue) return configuredValue.Value;

        if (definition.ValueCoefficient < 1)
        {
            throw new InvalidOperationException("Card value coefficient must be positive.");
        }

        var valueLevel = Math.Min(level, 4);
        var levelMultiplier = 1 << (valueLevel - 1);
        return checked(definition.ValueCoefficient * levelMultiplier * definition.Attributes.Identity.OccupiedSlots);
    }

    public static int CalculateAcquiredValue(int initialValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialValue);
        return initialValue / 2;
    }
}
