using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

public abstract record BattleEvent(BattleTick Tick);

public sealed record BattleStartedEvent(BattleTick Tick) : BattleEvent(Tick);

public sealed record AbilityQueuedEvent(
    BattleTick Tick,
    EntityId SourceCardId,
    SideId SourceSide,
    AbilitySourceKind SourceKind = AbilitySourceKind.Card) : BattleEvent(Tick);

public enum AbilitySourceKind
{
    Card = 0,
    Skill = 1,
    CardSet = 2,
}

public sealed record AbilityActivatedEvent(
    BattleTick Tick,
    EntityId SourceCardId,
    SideId SourceSide,
    bool IsEcho,
    AbilitySourceKind SourceKind = AbilitySourceKind.Card) : BattleEvent(Tick);

public sealed record ManaChangedEvent(BattleTick Tick, SideId Side, int Amount, int CurrentMana) : BattleEvent(Tick);

public sealed record StatusChangedEvent(BattleTick Tick, BattleStatus Status, int Amount) : BattleEvent(Tick);

public sealed record CardStateChangedEvent(BattleTick Tick, EntityId CardId, StringName StateKey, bool Enabled) : BattleEvent(Tick);

// 本场卡牌转变事实；不作为永久对局变化回写（领域战斗层）。
public sealed record CardTransformedEvent(BattleTick Tick, EntityId SourceCardId,
    EntityId TargetCardId, StringName ReplacementKey) : BattleEvent(Tick);

public sealed record CardSummonedEvent(BattleTick Tick, EntityId SourceCardId,
    EntityId SummonedCardId, SideId Side, StringName CardKey, int Level, int BoardStart) : BattleEvent(Tick);

public sealed record CardAttributeChangedEvent(
    BattleTick Tick,
    EntityId CardId,
    StringName AttributeKey,
    int Amount,
    int CurrentValue) : BattleEvent(Tick);

// 冻结自身发动任务的本场进度，临时来源不写回对局（领域战斗层）。
public sealed record CardQuestProgressChangedEvent(BattleTick Tick, EntityId CardId, SideId Side,
    StringName QuestKey, int Progress, bool Persists) : BattleEvent(Tick);

public sealed record CardChargedEvent(
    BattleTick Tick,
    EntityId SourceCardId,
    EntityId TargetCardId,
    int AmountTicks) : BattleEvent(Tick);

public sealed record CardDestroyedEvent(
    BattleTick Tick,
    EntityId CardId,
    SideId Side,
    EntityId SourceCardId) : BattleEvent(Tick);

public enum DamageSourceKind
{
    Card = 0,
    Status = 1,
    Eclipse = 2,
    Skill = 3,
    CardSet = 4,
}

public sealed record DamageDealtEvent(
    BattleTick Tick,
    EntityId SourceCardId,
    SideId TargetSide,
    int RawDamage,
    int ArmorAbsorbed,
    int HealthDamage,
    int RemainingHealth,
    DamageSourceKind SourceKind = DamageSourceKind.Card) : BattleEvent(Tick)
{
    // 冻结周期伤害的实际来源，回放不按Tick或生命差猜测灼伤／中毒。
    public BattleStatus? StatusOrigin { get; init; }
}

public sealed record HeroDefeatedEvent(BattleTick Tick, SideId Side) : BattleEvent(Tick);

public sealed record BattleEndedEvent(BattleTick Tick, BattleEndReason Reason) : BattleEvent(Tick);
