using System;

namespace Project_Star.Domain.Common;

/// <summary>Identifies one entity instance within a match.</summary>
public readonly record struct EntityId(Guid Value)
{
    public static EntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
