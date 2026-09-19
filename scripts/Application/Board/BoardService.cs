using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Board;

public sealed record BoardMove(
    EntityId CardId,
    BoardZone? FromZone,
    int? FromStart,
    BoardZone ToZone,
    int ToStart);

public sealed record BoardPlacementResult(
    PushDirection Direction,
    int TotalDistance,
    int AffectedCards,
    IReadOnlyList<BoardMove> Moves);

public sealed record BoardTarget(BoardZone Zone, int Start);

/// <summary>Validates ownership and atomically commits board placement plans.</summary>
public sealed class BoardService
{
    private static readonly StringName CardNotOwned = new("board.card_not_owned");
    private static readonly StringName CardNotPlaced = new("board.card_not_placed");
    private static readonly StringName PlacementFailed = new("board.placement_failed");

    private readonly BoardPlacementSolver _solver;

    public BoardService(BoardPlacementSolver solver)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
    }

    public Result<BoardPlacementResult> PlaceCard(
        MatchSession session,
        EntityId cardId,
        BoardZone targetZone,
        int targetStart)
    {
        ArgumentNullException.ThrowIfNull(session);
        var card = session.Player.Inventory.Find(cardId);
        if (card is null)
        {
            return Result<BoardPlacementResult>.Fail(
                new Failure(CardNotOwned, "Only a card owned by this match can be placed."));
        }

        var source = session.Board.Locate(cardId);
        var destination = session.Board.GetZone(targetZone);
        var plan = _solver.Solve(
            cardId,
            card.Attributes.Identity.OccupiedSlots,
            targetStart,
            destination.Capacity,
            destination.Placements);

        if (!plan.IsSuccess)
        {
            return Result<BoardPlacementResult>.Fail(
                new Failure(PlacementFailed, $"Placement failed: {plan.Failure}."));
        }

        var movingCardIsPushable = source?.Placement.IsPushable ?? true;
        var moves = new List<BoardMove>(plan.Shifts.Count + 1)
        {
            new(cardId, source?.Zone, source?.Placement.Start, targetZone, targetStart),
        };

        foreach (var shift in plan.Shifts)
        {
            moves.Add(new BoardMove(shift.CardId, targetZone, shift.FromStart, targetZone, shift.ToStart));
        }

        if (source is not null)
        {
            session.Board.GetZone(source.Value.Zone).Remove(cardId);
        }

        destination.Apply(
            cardId,
            card.Attributes.Identity.OccupiedSlots,
            targetStart,
            movingCardIsPushable,
            plan);

        return Result<BoardPlacementResult>.Success(
            new BoardPlacementResult(plan.Direction, plan.TotalDistance, plan.AffectedCards, moves));
    }

    public BoardTarget? FindFirstAvailableTarget(MatchSession session, int occupiedSlots)
    {
        ArgumentNullException.ThrowIfNull(session);
        var candidateId = EntityId.New();
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var zoneState = session.Board.GetZone(zone);
            for (var start = 0; start < zoneState.Capacity; start++)
            {
                var plan = _solver.Solve(
                    candidateId,
                    occupiedSlots,
                    start,
                    zoneState.Capacity,
                    zoneState.Placements);
                if (plan.IsSuccess)
                {
                    return new BoardTarget(zone, start);
                }
            }
        }

        return null;
    }

    public Result RemoveFromBoard(MatchSession session, EntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var location = session.Board.Locate(cardId);
        if (location is null)
        {
            return Result.Fail(new Failure(CardNotPlaced, "The card is not on either board zone."));
        }

        session.Board.GetZone(location.Value.Zone).Remove(cardId);
        return Result.Success();
    }

    public Result SetPushable(MatchSession session, EntityId cardId, bool isPushable)
    {
        ArgumentNullException.ThrowIfNull(session);
        var location = session.Board.Locate(cardId);
        if (location is null)
        {
            return Result.Fail(new Failure(CardNotPlaced, "The card is not on either board zone."));
        }

        session.Board.GetZone(location.Value.Zone).SetPushable(cardId, isPushable);
        return Result.Success();
    }
}
