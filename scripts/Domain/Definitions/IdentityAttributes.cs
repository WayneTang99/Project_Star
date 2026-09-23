using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Definitions;

public abstract class IdentityAttributes
{
    protected IdentityAttributes(StringName key, string displayName)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("Identity key cannot be empty.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        Key = key;
        DisplayName = displayName;
    }

    public StringName Key { get; }

    public string DisplayName { get; }
}

public sealed class HeroIdentityAttributes : IdentityAttributes
{
    public HeroIdentityAttributes(StringName key, string displayName, StringName factionKey)
        : base(key, displayName)
    {
        FactionKey = factionKey;
    }

    public StringName FactionKey { get; }
}

public sealed class CardIdentityAttributes : IdentityAttributes
{
    public CardIdentityAttributes(
        StringName key,
        string displayName,
        StringName factionKey,
        CardSize size,
        IEnumerable<StringName> elementKeys)
        : base(key, displayName)
    {
        if (!Enum.IsDefined(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, null);
        }

        FactionKey = factionKey;
        Size = size;
        ElementKeys = GameElements.Normalize(elementKeys);
    }

    public StringName FactionKey { get; }

    public CardSize Size { get; }

    public int OccupiedSlots => (int)Size;

    public IReadOnlyList<StringName> ElementKeys { get; }
}

public sealed class SkillIdentityAttributes : IdentityAttributes
{
    public SkillIdentityAttributes(StringName key, string displayName, StringName factionKey) : base(key, displayName)
    {
        FactionKey = factionKey;
    }

    public StringName FactionKey { get; }
}

public sealed class EncounterIdentityAttributes : IdentityAttributes
{
    public EncounterIdentityAttributes(StringName key, string displayName) : base(key, displayName)
    {
    }
}

// 怪物只读身份字段（领域定义层）。
public sealed class MonsterIdentityAttributes : IdentityAttributes
{
    public MonsterIdentityAttributes(StringName key, string displayName) : base(key, displayName)
    {
    }
}
