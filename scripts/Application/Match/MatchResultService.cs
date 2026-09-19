using System;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

public enum MatchBattleKind
{
    Monster = 0,
    Pvp = 1,
}

/// <summary>Applies only persistent battle consequences to the match aggregate.</summary>
public sealed class MatchResultService
{
    private readonly BoardService _boardService;

    public MatchResultService(BoardService boardService) => _boardService = boardService;

    public Result Apply(MatchSession session, BattleResult battle, MatchBattleKind kind, int? battleRound = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);
        if (session.Status != MatchStatus.InProgress)
            return Result.Fail(new Failure(new Godot.StringName("match.already_ended"), "The match has already ended."));

        ApplyPermanentChanges(session, battle);
        if (kind == MatchBattleKind.Monster)
        {
            if (battle.Outcome == BattleOutcome.PlayerVictory) session.Player.AddWealth(2);
            return Result.Success();
        }

        if (battle.Outcome == BattleOutcome.PlayerVictory) session.Progress.PvpWins++;
        else if (battle.Outcome == BattleOutcome.OpponentVictory)
            session.Player.LoseReputation(battleRound ?? session.Progress.Round);

        if (session.Progress.PvpWins >= 10) End(session, MatchStatus.Won);
        else if (session.Player.Reputation <= 0) End(session, MatchStatus.Lost);
        return Result.Success();
    }

    public Result Surrender(MatchSession session, MatchBattleKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (kind == MatchBattleKind.Pvp)
        {
            session.Player.LoseReputation(session.Progress.Round);
            if (session.Player.Reputation <= 0) End(session, MatchStatus.Lost);
        }
        return Result.Success();
    }

    private void ApplyPermanentChanges(MatchSession session, BattleResult battle)
    {
        foreach (var change in battle.PermanentChanges)
        {
            if (change.ChangeType != "Destroy") continue;
            if (session.Board.Contains(change.CardId)) _ = _boardService.RemoveFromBoard(session, change.CardId);
            session.Player.Inventory.Remove(change.CardId);
        }
    }

    private static void End(MatchSession session, MatchStatus status)
    {
        session.Status = status;
        session.Summary = new MatchSummary(
            status,
            session.Progress.Round,
            session.Progress.PvpWins,
            session.Player.Reputation,
            session.Player.Wealth);
    }
}
