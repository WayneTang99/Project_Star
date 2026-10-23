using System;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Domain.Match;
using Project_Star.Domain.Combat;
using Project_Star.Application.Encounters;

namespace Project_Star.Application.Match;

// 联网对局的访问状态容器（应用层）；与只保存页面状态的本地 Presenter 分开。
public sealed class ServerMatchSession(MatchSession match)
{
    public MatchSession Match { get; } = match ?? throw new ArgumentNullException(nameof(match));
    public ServerMatchPhase Phase { get; internal set; } = ServerMatchPhase.EncounterChoice;
    public StringName? ShopKey { get; internal set; }
    public ShopStock? Shop { get; internal set; }
    public ServerMonsterBattle? Battle { get; internal set; }
    public ServerEncounterVisit? Event { get; internal set; }
    public ServerPvpBattle? Pvp { get; internal set; }
}

// PvP任务冻结后的对手、种子与来源；状态由服务器任务和结算事务驱动（应用层）。
public sealed record ServerPvpBattle(Guid Id, int Round, ulong Seed, MatchSession Opponent,
    Guid? OpponentSnapshotId, bool IsTestOpponent, ServerBattleSummary? Result = null);

// 固定怪物实例、种子与发生回合；结算摘要随权威存档保存（应用层）。
public sealed record ServerMonsterBattle(Guid Id, StringName MonsterKey, int Round, int Turn,
    ulong Seed, MatchSession Opponent, ServerBattleSummary? Result = null, StringName? SourceEncounterKey = null);

// 固定本次事件身份、已展示选项和结算摘要；不序列化能力定义（应用层）。
public sealed record ServerEncounterVisit(Guid Id, StringName EncounterKey, int Round, int Turn,
    EncounterOptionSet Options, ServerEncounterOutcome? Result = null);

// 事件即时结果的持久摘要，避免持有可能已出售或合并的可变卡牌（应用层）。
public sealed record ServerEncounterOutcome(StringName OptionKey, EncounterAttributeChange[] Changes,
    int WealthGained, Guid? GrantedCardId, StringName? GrantedCardKey, int? GrantedCardLevel,
    bool CardRewardSkipped, int WealthSpent, int PendingBattleMaxHealthBonus,
    EncounterCardAttributeChange[] CardChanges, bool MonsterEncounterSkipped);

// 小型结果摘要不包含完整回放；奖励、成长与随机状态保存在对局聚合中（应用层）。
public sealed record ServerBattleSummary(BattleOutcome Outcome, BattleEndReason EndReason, long EndedAt,
    int PlayerRemainingHealth, int OpponentRemainingHealth);

public enum ServerMatchPhase
{
    EncounterChoice = 0,
    Shop = 1,
    MonsterPreparation = 2,
    MonsterResult = 3,
    Event = 4,
    PvpPending = 5,
    PvpResult = 6,
}
