using System;

namespace Project_Star.Domain.Common;

/// <summary>Separates immutable identity from persistent and base-combat values.</summary>
public sealed class EntityAttributes<TIdentity> where TIdentity : class
{
    public EntityAttributes(
        TIdentity identity,
        ModifiableAttributeSet? persistent = null,
        ModifiableAttributeSet? baseCombat = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Persistent = persistent ?? new ModifiableAttributeSet();
        BaseCombat = baseCombat ?? new ModifiableAttributeSet();
    }

    public TIdentity Identity { get; }

    public ModifiableAttributeSet Persistent { get; }

    public ModifiableAttributeSet BaseCombat { get; }
}
