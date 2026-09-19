using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

public sealed record PermanentChange(EntityId CardId, string ChangeType);

public enum BattleOutcome
{
    PlayerVictory = 0,
    OpponentVictory = 1,
    Draw = 2,
}

public enum BattleEndReason
{
    HeroDefeated = 0,
    SimultaneousDefeat = 1,
    Timeout = 2,
    Extinction = 3,
}

public sealed class BattleResult
{
    public BattleResult(
        BattleOutcome outcome,
        BattleEndReason endReason,
        BattleTick endedAt,
        int playerRemainingHealth,
        int opponentRemainingHealth,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<PermanentChange>? permanentChanges = null)
    {
        Outcome = outcome;
        EndReason = endReason;
        EndedAt = endedAt;
        PlayerRemainingHealth = playerRemainingHealth;
        OpponentRemainingHealth = opponentRemainingHealth;
        Events = events ?? throw new ArgumentNullException(nameof(events));
        PermanentChanges = permanentChanges ?? Array.Empty<PermanentChange>();
    }

    public BattleOutcome Outcome { get; }

    public BattleEndReason EndReason { get; }

    public BattleTick EndedAt { get; }

    public int PlayerRemainingHealth { get; }

    public int OpponentRemainingHealth { get; }

    public IReadOnlyList<BattleEvent> Events { get; }

    public IReadOnlyList<PermanentChange> PermanentChanges { get; }
}
