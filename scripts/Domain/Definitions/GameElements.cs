using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Definitions;

/// <summary>Defines every legal card element and its canonical order.</summary>
public static class GameElements
{
    public static readonly StringName General = new("General");
    public static readonly StringName Fire = new("Fire");
    public static readonly StringName Water = new("Water");
    public static readonly StringName Wind = new("Wind");
    public static readonly StringName Earth = new("Earth");
    public static readonly StringName Lightning = new("Lightning");
    public static readonly StringName Wood = new("Wood");
    public static readonly StringName Ice = new("Ice");
    public static readonly StringName Light = new("Light");
    public static readonly StringName Dark = new("Dark");

    private static readonly StringName[] CanonicalValues =
    [
        General, Fire, Water, Wind, Earth, Lightning, Wood, Ice, Light, Dark,
    ];

    public static IReadOnlyList<StringName> Normalize(IEnumerable<StringName> elementKeys)
    {
        ArgumentNullException.ThrowIfNull(elementKeys);
        var selected = new HashSet<StringName>();
        foreach (var elementKey in elementKeys)
        {
            if (!IsValid(elementKey))
            {
                throw new ArgumentException($"Unknown element '{elementKey}'.", nameof(elementKeys));
            }

            if (!selected.Add(elementKey))
            {
                throw new ArgumentException($"Element '{elementKey}' is duplicated.", nameof(elementKeys));
            }
        }

        if (selected.Count is < 1 or > 2)
        {
            throw new ArgumentException("A card must have one or two elements.", nameof(elementKeys));
        }

        var normalized = new List<StringName>(selected.Count);
        foreach (var elementKey in CanonicalValues)
        {
            if (selected.Contains(elementKey))
            {
                normalized.Add(elementKey);
            }
        }

        return normalized.AsReadOnly();
    }

    public static bool IsValid(StringName elementKey)
    {
        foreach (var candidate in CanonicalValues)
        {
            if (candidate == elementKey)
            {
                return true;
            }
        }

        return false;
    }
}
