using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Common;

/// <summary>Stores base integer values separately from traceable additive modifiers.</summary>
public sealed class ModifiableAttributeSet
{
    private readonly Dictionary<StringName, int> _baseValues;
    private readonly Dictionary<ModifierId, StatModifier> _modifiers = [];
    private bool _isReadOnly;

    public ModifiableAttributeSet(IReadOnlyDictionary<StringName, int>? baseValues = null)
    {
        _baseValues = baseValues is null ? [] : new Dictionary<StringName, int>(baseValues);
    }

    public int GetBaseValue(StringName attributeKey) =>
        _baseValues.TryGetValue(attributeKey, out var value) ? value : 0;

    public int GetFinalValue(StringName attributeKey)
    {
        var value = GetBaseValue(attributeKey);
        foreach (var modifier in _modifiers.Values)
        {
            if (modifier.AttributeKey == attributeKey)
            {
                value = checked(value + modifier.Amount);
            }
        }

        return value;
    }

    public void SetBaseValue(StringName attributeKey, int value)
    {
        EnsureMutable();
        _baseValues[attributeKey] = value;
    }

    public void ApplyModifier(StatModifier modifier)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(modifier);
        if (!_modifiers.TryAdd(modifier.Id, modifier))
        {
            throw new InvalidOperationException($"Modifier '{modifier.Id}' has already been applied.");
        }
    }

    public bool RemoveModifier(ModifierId modifierId)
    {
        EnsureMutable();
        return _modifiers.Remove(modifierId);
    }

    public void Freeze() => _isReadOnly = true;

    public ModifiableAttributeSet CreateMutableCopy() => new(_baseValues);

    private void EnsureMutable()
    {
        if (_isReadOnly)
        {
            throw new InvalidOperationException("Definition attributes are read-only.");
        }
    }
}
