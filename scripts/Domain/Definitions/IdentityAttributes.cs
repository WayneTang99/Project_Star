using System;
using System.Collections.Generic;
using System.Linq;
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
    public HeroIdentityAttributes(StringName key, string displayName, StringName factionKey, string title = "", StringName? illustration = null)
        : base(key, displayName)
    {
        FactionKey = factionKey;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Illustration = illustration ?? new StringName("");
    }

    public StringName FactionKey { get; }

    public string Title { get; }
    public StringName Illustration { get; }
}

// 卡牌只读身份字段（领域定义层）。
public sealed class CardIdentityAttributes : IdentityAttributes
{
    // 创建卡牌身份；验证夹具可省略插画和展示描述。
    public CardIdentityAttributes(
        StringName key,
        string displayName,
        StringName factionKey,
        CardSize size,
        IEnumerable<StringName> elementKeys,
        StringName? setKey = null,
        StringName? illustration = null,
        IEnumerable<CardDescriptionEntry>? descriptionEntries = null,
        int gemSocketCount = 1)
        : base(key, displayName)
    {
        if (!Enum.IsDefined(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, null);
        }
        if (setKey is { IsEmpty: true })
            throw new ArgumentException("Set key cannot be empty.", nameof(setKey));

        FactionKey = factionKey;
        Size = size;
        ElementKeys = GameElements.Normalize(elementKeys);
        SetKey = setKey;
        Illustration = illustration ?? new StringName("");
        var entries = (descriptionEntries ?? []).ToArray();
        if (entries.Any(entry => entry is null))
            throw new ArgumentException("Description entries cannot contain null.", nameof(descriptionEntries));
        DescriptionEntries = Array.AsReadOnly(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(gemSocketCount);
        GemSocketCount = gemSocketCount;
    }

    public StringName FactionKey { get; }

    public CardSize Size { get; }

    public int OccupiedSlots => (int)Size;

    public IReadOnlyList<StringName> ElementKeys { get; }

    public StringName? SetKey { get; }

    // 插画资源标识属于只读身份；纹理由表现层加载。
    public StringName Illustration { get; }

    // 展示描述属于只读文本，由内容定义提供，不参与规则计算。
    public IReadOnlyList<CardDescriptionEntry> DescriptionEntries { get; }
    public int GemSocketCount { get; }
}

// 宝石只读身份及展示说明（领域定义层）。
public sealed class GemIdentityAttributes : IdentityAttributes
{
    public GemIdentityAttributes(StringName key, string displayName, string description = "") : base(key, displayName)
        => Description = description ?? throw new ArgumentNullException(nameof(description));
    public string Description { get; }
}

// 套装只读身份字段（领域定义层）。
public sealed class CardSetIdentityAttributes : IdentityAttributes
{
    public CardSetIdentityAttributes(StringName key, string displayName) : base(key, displayName)
    {
    }
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
