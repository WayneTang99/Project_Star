using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

/// <summary>Read-only presentation data for one card face. It does not own game state.</summary>
public sealed class CardFaceViewModel
{
    public CardFaceViewModel(
        StringName cardKey,
        string displayName,
        StringName factionKey,
        CardSize size,
        int level,
        int currentValue,
        Texture2D? artwork,
        IEnumerable<StringName> elementKeys,
        IEnumerable<CardFaceEffect> effects)
    {
        if (cardKey.IsEmpty) throw new ArgumentException("Card key cannot be empty.", nameof(cardKey));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        if (!Enum.IsDefined(size)) throw new ArgumentOutOfRangeException(nameof(size));
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentOutOfRangeException.ThrowIfNegative(currentValue);
        ArgumentNullException.ThrowIfNull(elementKeys);
        ArgumentNullException.ThrowIfNull(effects);

        var normalizedElements = GameElements.Normalize(elementKeys);
        var configuredEffects = effects.ToArray();
        if (configuredEffects.Any(effect => effect is null || string.IsNullOrWhiteSpace(effect.Value)))
            throw new ArgumentException("Card effects must have a display value.", nameof(effects));

        CardKey = cardKey;
        DisplayName = displayName;
        FactionKey = factionKey;
        Size = size;
        Level = level;
        CurrentValue = currentValue;
        Artwork = artwork;
        ElementKeys = normalizedElements;
        Effects = Array.AsReadOnly(configuredEffects);
    }

    public StringName CardKey { get; }
    public string DisplayName { get; }
    public StringName FactionKey { get; }
    public CardSize Size { get; }
    public int Level { get; }
    public int CurrentValue { get; }
    public Texture2D? Artwork { get; }
    public IReadOnlyList<StringName> ElementKeys { get; }
    public IReadOnlyList<CardFaceEffect> Effects { get; }
    public IReadOnlyList<string?> GemNames { get; init; } = Array.AsReadOnly(new string?[1]);
}
