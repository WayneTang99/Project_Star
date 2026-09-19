namespace Project_Star.Domain.Common;

/// <summary>Supplies deterministic random values to domain rules.</summary>
public interface IRandomSource
{
    ulong State { get; }

    int NextInt(int minInclusive, int maxExclusive);
}
