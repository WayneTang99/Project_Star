using Godot;

namespace Project_Star.Core.States;

public class HeroAttributeSet : AttributeSet
{
    public StringName HeroKey { get; }
    public string HeroDisplayName { get; }
    public StringName FactionKey { get; }

    public int Wealth { get => GetValue(nameof(Wealth)); set => SetValue(nameof(Wealth), value); }
    public int Experience { get => GetValue(nameof(Experience)); set => SetValue(nameof(Experience), value); }
    public int Level { get => GetValue(nameof(Level)); set => SetValue(nameof(Level), value); }
    public int Reputation { get => GetValue(nameof(Reputation)); set => SetValue(nameof(Reputation), value); }

    public HeroAttributeSet(StringName heroKey, string heroDisplayName, StringName factionKey)
    {
        HeroKey = heroKey;
        HeroDisplayName = heroDisplayName;
        FactionKey = factionKey;
    }
}