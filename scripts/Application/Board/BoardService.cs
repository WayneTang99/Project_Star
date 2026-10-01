using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
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
    private readonly CardSetBonusService? _setBonuses;
    private readonly CardQuestBonusService _questBonuses = new();

    public BoardService(BoardPlacementSolver solver, IReadOnlyDictionary<StringName, CardSetDefinition>? sets = null)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _setBonuses = sets is null ? null : new CardSetBonusService(sets);
    }

    // 只读预览完整推挤计划；不修改棋盘、任务、套装或随机状态。
    public Result<BoardPlacementResult> PreviewPlaceCard(
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
                new Failure(PlacementFailed, plan.Failure switch
                {
                    PlacementFailure.OutsideBoard => "目标超出棋盘边界。",
                    PlacementFailure.BlockedByUnpushableCard => "目标被不可推挤卡牌阻挡。",
                    _ => "目标区域没有足够空间。",
                }));
        }

        var moves = new List<BoardMove>(plan.Shifts.Count + 1)
        {
            new(cardId, source?.Zone, source?.Placement.Start, targetZone, targetStart),
        };

        foreach (var shift in plan.Shifts)
        {
            moves.Add(new BoardMove(shift.CardId, targetZone, shift.FromStart, targetZone, shift.ToStart));
        }

        return Result<BoardPlacementResult>.Success(
            new BoardPlacementResult(plan.Direction, plan.TotalDistance, plan.AffectedCards, moves.AsReadOnly()));
    }

    // 提交时重新预览当前棋盘，验证通过后原子应用完整位移。
    public Result<BoardPlacementResult> PlaceCard(MatchSession session, EntityId cardId, BoardZone targetZone, int targetStart)
    {
        var preview = PreviewPlaceCard(session, cardId, targetZone, targetStart);
        if (preview.IsFailure) return preview;
        var card = session.Player.Inventory.Find(cardId)!;
        var source = session.Board.Locate(cardId);
        var destination = session.Board.GetZone(targetZone);
        var plan = _solver.Solve(cardId, card.Attributes.Identity.OccupiedSlots, targetStart,
            destination.Capacity, destination.Placements);
        var movingCardIsPushable = source?.Placement.IsPushable ?? true;

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
        _setBonuses?.Recalculate(session);
        _questBonuses.Recalculate(session);

        return preview;
    }

    public BoardTarget? FindFirstAvailableTarget(MatchSession session, int occupiedSlots,
        IReadOnlyCollection<EntityId>? removedCards = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var candidateId = EntityId.New();
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var zoneState = session.Board.GetZone(zone);
            var placements = new List<BoardPlacement>();
            foreach (var placement in zoneState.Placements)
                if (removedCards is null || !System.Linq.Enumerable.Contains(removedCards, placement.CardId))
                    placements.Add(placement);
            for (var start = 0; start < zoneState.Capacity; start++)
            {
                var plan = _solver.Solve(
                    candidateId,
                    occupiedSlots,
                    start,
                    zoneState.Capacity,
                    placements);
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
        _setBonuses?.Recalculate(session);
        _questBonuses.Recalculate(session);
        return Result.Success();
    }

    // 任务解锁后刷新当前战场的持续属性能力。
    public void RefreshQuestAbilities(MatchSession session) => _questBonuses.Recalculate(session);

    // 合并改变卡牌等级后，重新应用套装与已解锁任务的持续能力。
    public void RefreshBonuses(MatchSession session)
    {
        _setBonuses?.Recalculate(session);
        _questBonuses.Recalculate(session);
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
