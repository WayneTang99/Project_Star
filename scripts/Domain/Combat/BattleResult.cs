using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

public sealed record PermanentChange(EntityId CardId, string ChangeType);

// 战斗中已生效、结算时需保留到对局实例的属性贡献（领域战斗层）。
public sealed record PermanentCardAttributeBonus(EntityId CardId, SideId Side, Godot.StringName AttributeKey, int Amount);

// 开战时冻结的己方对局奖励来源，战斗不执行其效果（领域战斗输入输出）。
public sealed record MatchRewardSource(EntityId SourceId, int Level, bool RequiresOwnedCard, IReadOnlyList<AbilityDefinition> Abilities);

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

// 战斗领域的结算结果与冻结记录，播放不再修改此结果。
public sealed class BattleResult
{
    public BattleResult(
        BattleOutcome outcome,
        BattleEndReason endReason,
        BattleTick endedAt,
        int playerRemainingHealth,
        int opponentRemainingHealth,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<PermanentChange>? permanentChanges = null,
        IReadOnlyList<BattleStateSnapshot>? states = null,
        IReadOnlyList<PermanentCardAttributeBonus>? permanentAttributeBonuses = null)
    {
        Outcome = outcome;
        EndReason = endReason;
        EndedAt = endedAt;
        PlayerRemainingHealth = playerRemainingHealth;
        OpponentRemainingHealth = opponentRemainingHealth;
        Events = events ?? throw new ArgumentNullException(nameof(events));
        PermanentChanges = permanentChanges ?? Array.Empty<PermanentChange>();
        States = states ?? Array.Empty<BattleStateSnapshot>();
        PermanentAttributeBonuses = permanentAttributeBonuses ?? Array.Empty<PermanentCardAttributeBonus>();
    }

    public BattleOutcome Outcome { get; }

    public BattleEndReason EndReason { get; }

    public BattleTick EndedAt { get; }

    public int PlayerRemainingHealth { get; }

    public int OpponentRemainingHealth { get; }

    public IReadOnlyList<BattleEvent> Events { get; }

    public IReadOnlyList<PermanentChange> PermanentChanges { get; }

    public IReadOnlyList<BattleStateSnapshot> States { get; }
    public IReadOnlyList<PermanentCardAttributeBonus> PermanentAttributeBonuses { get; }
    public IReadOnlyList<MatchRewardSource> MatchRewardSources { get; init; } = Array.Empty<MatchRewardSource>();
}
