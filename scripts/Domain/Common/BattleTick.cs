using System;

namespace Project_Star.Domain.Common;

/// <summary>Represents deterministic battle time in fixed 0.1-second steps.</summary>
public readonly record struct BattleTick : IComparable<BattleTick>
{
    public const int TicksPerSecond = 10;

    public BattleTick(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public long Value { get; }

    public static BattleTick Zero => new(0);

    public static BattleTick FromPeriodicSeconds(decimal seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        var ticks = seconds * TicksPerSecond;
        if (ticks != decimal.Truncate(ticks))
        {
            throw new ArgumentException("Seconds must align to the 0.1-second battle step.", nameof(seconds));
        }

        return new BattleTick(decimal.ToInt64(ticks));
    }

    public decimal ToSeconds() => Value / (decimal)TicksPerSecond;

    public int CompareTo(BattleTick other) => Value.CompareTo(other.Value);

    public static BattleTick operator +(BattleTick left, BattleTick right) => new(checked(left.Value + right.Value));
}
