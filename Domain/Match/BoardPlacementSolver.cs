using System;
using System.Collections.Generic;
using System.Linq;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Match;

/// <summary>Calculates a complete board placement without mutating board state.</summary>
public sealed class BoardPlacementSolver
{
    public PlacementPlan Solve(
        EntityId movingCardId,
        int movingSize,
        int targetStart,
        int capacity,
        IReadOnlyList<BoardPlacement> currentPlacements)
    {
        if (movingSize < 1 || targetStart < 0 || targetStart + movingSize > capacity)
        {
            return PlacementPlan.Fail(targetStart, PlacementFailure.OutsideBoard);
        }

        var placements = currentPlacements
            .Where(placement => placement.CardId != movingCardId)
            .OrderBy(placement => placement.Start)
            .ToArray();

        if (!OverlapsAny(targetStart, movingSize, placements))
        {
            return PlacementPlan.Success(targetStart, PushDirection.None, 0, Array.Empty<PlacementShift>());
        }

        var right = SimulateRight(targetStart, movingSize, capacity, placements);
        var left = SimulateLeft(targetStart, movingSize, placements);
        if (!right.IsSuccess && !left.IsSuccess)
        {
            var failure = right.Failure == PlacementFailure.BlockedByUnpushableCard
                || left.Failure == PlacementFailure.BlockedByUnpushableCard
                ? PlacementFailure.BlockedByUnpushableCard
                : PlacementFailure.InsufficientSpace;
            return PlacementPlan.Fail(targetStart, failure);
        }

        if (!left.IsSuccess)
        {
            return right;
        }

        if (!right.IsSuccess)
        {
            return left;
        }

        if (right.TotalDistance != left.TotalDistance)
        {
            return right.TotalDistance < left.TotalDistance ? right : left;
        }

        if (right.AffectedCards != left.AffectedCards)
        {
            return right.AffectedCards < left.AffectedCards ? right : left;
        }

        return right;
    }

    private static PlacementPlan SimulateRight(
        int targetStart,
        int movingSize,
        int capacity,
        IReadOnlyList<BoardPlacement> placements)
    {
        var shifts = new List<PlacementShift>();
        var occupiedUntil = targetStart + movingSize;

        foreach (var placement in placements)
        {
            if (placement.EndExclusive <= targetStart || placement.Start >= occupiedUntil)
            {
                continue;
            }

            if (!placement.IsPushable)
            {
                return PlacementPlan.Fail(targetStart, PlacementFailure.BlockedByUnpushableCard);
            }

            var newStart = occupiedUntil;
            if (newStart + placement.Size > capacity)
            {
                return PlacementPlan.Fail(targetStart, PlacementFailure.InsufficientSpace);
            }

            shifts.Add(new PlacementShift(placement.CardId, placement.Start, newStart));
            occupiedUntil = newStart + placement.Size;
        }

        return CreateSuccess(targetStart, PushDirection.Right, shifts);
    }

    private static PlacementPlan SimulateLeft(
        int targetStart,
        int movingSize,
        IReadOnlyList<BoardPlacement> placements)
    {
        var shifts = new List<PlacementShift>();
        var occupiedFrom = targetStart;
        var targetEnd = targetStart + movingSize;

        for (var index = placements.Count - 1; index >= 0; index--)
        {
            var placement = placements[index];
            if (placement.Start >= targetEnd || placement.EndExclusive <= occupiedFrom)
            {
                continue;
            }

            if (!placement.IsPushable)
            {
                return PlacementPlan.Fail(targetStart, PlacementFailure.BlockedByUnpushableCard);
            }

            var newStart = occupiedFrom - placement.Size;
            if (newStart < 0)
            {
                return PlacementPlan.Fail(targetStart, PlacementFailure.InsufficientSpace);
            }

            shifts.Add(new PlacementShift(placement.CardId, placement.Start, newStart));
            occupiedFrom = newStart;
        }

        shifts.Reverse();
        return CreateSuccess(targetStart, PushDirection.Left, shifts);
    }

    private static PlacementPlan CreateSuccess(
        int targetStart,
        PushDirection direction,
        IReadOnlyList<PlacementShift> shifts)
    {
        var distance = 0;
        foreach (var shift in shifts)
        {
            distance = checked(distance + Math.Abs(shift.ToStart - shift.FromStart));
        }

        return PlacementPlan.Success(targetStart, direction, distance, shifts);
    }

    private static bool OverlapsAny(
        int targetStart,
        int movingSize,
        IReadOnlyList<BoardPlacement> placements)
    {
        var targetEnd = targetStart + movingSize;
        foreach (var placement in placements)
        {
            if (targetStart < placement.EndExclusive && placement.Start < targetEnd)
            {
                return true;
            }
        }

        return false;
    }
}
