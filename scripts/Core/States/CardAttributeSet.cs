using System.Collections.Generic;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.States;

public class CardAttributeSet : AttributeSet
{
    public StringName CardKey { get; }
    public string DisplayName { get; }
    public StringName FactionKey { get; }
    public CardSize Size { get; }

    public int Level { get => GetValue(nameof(Level)); set => SetValue(nameof(Level), value); }
    public int Value { get => GetValue(nameof(Value)); set => SetValue(nameof(Value), value); }
    public int PersistentBonus { get => GetValue(nameof(PersistentBonus)); set => SetValue(nameof(PersistentBonus), value); }

    public HashSet<StringName> UnlockRecord { get; } = new();

    public CardAttributeSet(StringName cardKey, string displayName, StringName factionKey, CardSize size)
    {
        CardKey = cardKey;
        DisplayName = displayName;
        FactionKey = factionKey;
        Size = size;
    }

    public override AttributeSet Clone()
    {
        var copy = (CardAttributeSet)base.Clone();
        copy.UnlockRecord.Clear();
        foreach (var key in UnlockRecord)
            copy.UnlockRecord.Add(key);
        return copy;
    }
}