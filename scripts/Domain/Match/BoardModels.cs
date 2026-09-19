using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Match;

public enum BoardZone
{
    Battlefield = 0,
    Bench = 1,
}

public enum PushDirection
{
    None = 0,
    Left = 1,
    Right = 2,
}

public sealed record BoardPlacement(EntityId CardId, int Start, int Size, bool IsPushable)
{
    public int EndExclusive => checked(Start + Size);
}

public sealed record PlacementShift(EntityId CardId, int FromStart, int ToStart);

public enum PlacementFailure
{
    None = 0,
    OutsideBoard = 1,
    BlockedByUnpushableCard = 2,
    InsufficientSpace = 3,
}

public sealed class PlacementPlan
{
    private PlacementPlan(
        bool isSuccess,
        int targetStart,
        PushDirection direction,
        int totalDistance,
        IReadOnlyList<PlacementShift> shifts,
        PlacementFailure failure)
    {
        IsSuccess = isSuccess;
        TargetStart = targetStart;
        Direction = direction;
        TotalDistance = totalDistance;
        Shifts = shifts;
        Failure = failure;
    }

    public bool IsSuccess { get; }

    public int TargetStart { get; }

    public PushDirection Direction { get; }

    public int TotalDistance { get; }

    public int AffectedCards => Shifts.Count;

    public IReadOnlyList<PlacementShift> Shifts { get; }

    public PlacementFailure Failure { get; }

    public static PlacementPlan Success(
        int targetStart,
        PushDirection direction,
        int totalDistance,
        IReadOnlyList<PlacementShift> shifts) =>
        new(true, targetStart, direction, totalDistance, shifts, PlacementFailure.None);

    public static PlacementPlan Fail(int targetStart, PlacementFailure failure) =>
        new(false, targetStart, PushDirection.None, 0, Array.Empty<PlacementShift>(), failure);
}

public sealed class BoardZoneState
{
    private readonly List<BoardPlacement> _placements = [];

    public BoardZoneState(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => _placements.Count;

    public IReadOnlyList<BoardPlacement> Placements => _placements;

    public BoardPlacement? Find(EntityId cardId)
    {
        foreach (var placement in _placements)
        {
            if (placement.CardId == cardId)
            {
                return placement;
            }
        }

        return null;
    }

    internal bool Remove(EntityId cardId)
    {
        var placement = Find(cardId);
        return placement is not null && _placements.Remove(placement);
    }

    internal void Apply(EntityId cardId, int size, int targetStart, bool isPushable, PlacementPlan plan)
    {
        foreach (var shift in plan.Shifts)
        {
            var existing = Find(shift.CardId)
                ?? throw new InvalidOperationException($"Board card '{shift.CardId}' was not found while applying a plan.");
            _placements.Remove(existing);
            _placements.Add(existing with { Start = shift.ToStart });
        }

        _placements.Add(new BoardPlacement(cardId, targetStart, size, isPushable));
        _placements.Sort((left, right) => left.Start.CompareTo(right.Start));
    }

    internal bool SetPushable(EntityId cardId, bool isPushable)
    {
        var existing = Find(cardId);
        if (existing is null)
        {
            return false;
        }

        _placements.Remove(existing);
        _placements.Add(existing with { IsPushable = isPushable });
        _placements.Sort((left, right) => left.Start.CompareTo(right.Start));
        return true;
    }
}

public sealed class BoardState
{
    public const int DefaultCapacity = 10;

    public BoardState(int capacity = DefaultCapacity)
    {
        Battlefield = new BoardZoneState(capacity);
        Bench = new BoardZoneState(capacity);
    }

    public BoardZoneState Battlefield { get; }

    public BoardZoneState Bench { get; }

    public bool Contains(EntityId id) => Battlefield.Find(id) is not null || Bench.Find(id) is not null;

    public (BoardZone Zone, BoardPlacement Placement)? Locate(EntityId id)
    {
        var battlefield = Battlefield.Find(id);
        if (battlefield is not null)
        {
            return (BoardZone.Battlefield, battlefield);
        }

        var bench = Bench.Find(id);
        return bench is null ? null : (BoardZone.Bench, bench);
    }

    public BoardZoneState GetZone(BoardZone zone) => zone switch
    {
        BoardZone.Battlefield => Battlefield,
        BoardZone.Bench => Bench,
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, null),
    };
}
