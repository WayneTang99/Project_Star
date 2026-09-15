using System.Collections.Generic;
using Godot;

namespace Project_Star.Core.States;

public abstract class BattleState
{
    private Dictionary<StringName, int> _values = new();

    public int GetValue(StringName key) => _values.GetValueOrDefault(key, 0);

    public void SetValue(StringName key, int value) => _values[key] = value;

    public void ApplyModifier(StringName key, int amount) => _values[key] = GetValue(key) + amount;

    public void RemoveModifier(StringName key, int amount) => _values[key] = GetValue(key) - amount;

    public virtual BattleState Clone()
    {
        var copy = (BattleState)MemberwiseClone();
        copy._values = new Dictionary<StringName, int>(_values);
        return copy;
    }
}