using System;
using Project_Star.Domain.Common;

namespace Project_Star.Infrastructure.Random;

/// <summary>A small deterministic random generator whose complete state can be persisted.</summary>
public sealed class SeededRandom : IRandomSource
{
    public SeededRandom(ulong seed)
    {
        State = seed;
    }

    public ulong State { get; private set; }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        var value = NextUInt64();
        return (int)(minInclusive + (long)(value % range));
    }

    private ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15UL;
        var value = State;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
