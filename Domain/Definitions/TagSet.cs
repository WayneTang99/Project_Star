using System.Collections;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Definitions;

/// <summary>An immutable set of classification keys.</summary>
public sealed class TagSet : IReadOnlyCollection<StringName>
{
    private readonly HashSet<StringName> _values;

    public TagSet(IEnumerable<StringName>? values = null)
    {
        _values = values is null ? [] : new HashSet<StringName>(values);
    }

    public int Count => _values.Count;

    public bool Contains(StringName value) => _values.Contains(value);

    public static TagSet ForCard(CardSize size, IEnumerable<StringName>? values = null)
    {
        var tags = values is null ? [] : new HashSet<StringName>(values);
        tags.Add(GameTags.FromSize(size));
        return new TagSet(tags);
    }

    public IEnumerator<StringName> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
