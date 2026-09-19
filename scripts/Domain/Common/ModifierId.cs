using System;

namespace Project_Star.Domain.Common;

/// <summary>Identifies one independently removable attribute contribution.</summary>
public readonly record struct ModifierId(Guid Value)
{
    public static ModifierId New() => new(Guid.NewGuid());
}
