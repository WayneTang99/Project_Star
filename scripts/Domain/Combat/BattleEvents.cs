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

public sealed record CardAttributeChangedEvent(
    BattleTick Tick,
    EntityId CardId,
    StringName AttributeKey,
    int Amount,
    int CurrentValue) : BattleEvent(Tick);

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
    DamageSourceKind SourceKind = DamageSourceKind.Card) : BattleEvent(Tick);

public sealed record HeroDefeatedEvent(BattleTick Tick, SideId Side) : BattleEvent(Tick);

public sealed record BattleEndedEvent(BattleTick Tick, BattleEndReason Reason) : BattleEvent(Tick);
