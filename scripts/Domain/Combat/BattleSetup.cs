using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

public sealed record HeroBattleSetup(
    EntityId EntityId,
    int MaxHealth,
    int Armor,
    int MaxMana = 100,
    int InitialMana = 0,
    int ManaRegen = 10,
    int HealthRegen = 0,
    int Burn = 0,
    int Poison = 0);

public sealed record CardBattleSetup(
    EntityId EntityId,
    int BoardStart,
    int AttackDamage,
    int CooldownTicks,
    IReadOnlyList<AbilityDefinition>? Abilities = null,
    bool IsOnBench = false,
    bool UseLegacyAttack = true);

public sealed class BattleSideSetup
{
    public BattleSideSetup(HeroBattleSetup hero, IReadOnlyList<CardBattleSetup> cards)
    {
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        Cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public HeroBattleSetup Hero { get; }

    public IReadOnlyList<CardBattleSetup> Cards { get; }
}

/// <summary>An immutable snapshot containing every input used by one battle simulation.</summary>
public sealed class BattleSetup
{
    public BattleSetup(BattleSideSetup player, BattleSideSetup opponent, ulong seed, BattleTick timeout, BattleTick? eclipseTime = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Opponent = opponent ?? throw new ArgumentNullException(nameof(opponent));
        Seed = seed;
        Timeout = timeout;
        EclipseTime = eclipseTime ?? timeout;
    }

    public BattleSideSetup Player { get; }

    public BattleSideSetup Opponent { get; }

    public ulong Seed { get; }

    public BattleTick Timeout { get; }
    public BattleTick EclipseTime { get; }
}
