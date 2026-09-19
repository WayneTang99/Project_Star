using Godot;

namespace Project_Star.Domain.Common;

/// <summary>Describes an additive contribution from one source to one attribute.</summary>
public sealed record StatModifier(
    ModifierId Id,
    EntityId SourceId,
    StringName AttributeKey,
    int Amount,
    ModifierLifetime Lifetime = ModifierLifetime.UntilRemoved);

public enum ModifierLifetime
{
    UntilRemoved = 0,
    Battle = 1,
    Timed = 2,
}
