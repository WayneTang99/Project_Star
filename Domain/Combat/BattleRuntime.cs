using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

internal sealed class HeroBattleState
{
    public HeroBattleState(HeroBattleSetup setup)
    {
        EntityId = setup.EntityId; MaxHealth = setup.MaxHealth; Health = setup.MaxHealth; Armor = setup.Armor;
        MaxMana = setup.MaxMana; Mana = setup.InitialMana; ManaRegen = setup.ManaRegen;
        HealthRegen = setup.HealthRegen; Burn = setup.Burn; Poison = setup.Poison;
    }
    public EntityId EntityId { get; }
    public int MaxHealth { get; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public int MaxMana { get; }
    public int Mana { get; set; }
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int HealthRegen { get; set; }
    public int ManaRegen { get; set; }
}

internal sealed class BattleAbilityState
{
    public BattleAbilityState(AbilityDefinition definition)
    {
        Definition = definition; RemainingCooldownUnits = definition.CooldownTicks * 2;
    }
    public AbilityDefinition Definition { get; }
    public int RemainingCooldownUnits { get; set; }
}

internal sealed class CardBattleState
{
    public CardBattleState(CardBattleSetup setup, SideId side)
    {
        EntityId = setup.EntityId; Side = side; BoardStart = setup.BoardStart; IsOnBench = setup.IsOnBench;
        var definitions = setup.Abilities is { Count: > 0 } ? setup.Abilities :
        [new AbilityDefinition(new StringName("ability.legacy_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, setup.CooldownTicks, [new DamageEffectDefinition(setup.AttackDamage)])];
        foreach (var definition in definitions) Abilities.Add(new BattleAbilityState(definition));
    }
    public EntityId EntityId { get; }
    public SideId Side { get; }
    public int BoardStart { get; }
    public bool IsOnBench { get; }
    public bool Destroyed { get; set; }
    public int HasteDuration { get; set; }
    public int SlowDuration { get; set; }
    public int ImmobilizeDuration { get; set; }
    public List<BattleAbilityState> Abilities { get; } = [];
}

internal sealed record PendingAbility(CardBattleState Source, BattleAbilityState Ability, bool IsEcho, BattleTick EnqueuedAt);

internal sealed class AbilityQueue
{
    private readonly Queue<PendingAbility> _items = new();
    public int Count => _items.Count;
    public void Enqueue(PendingAbility ability) => _items.Enqueue(ability);
    public PendingAbility Dequeue() => _items.Dequeue();
}

internal sealed class BattleRuntime
{
    public BattleRuntime(BattleSetup setup)
    {
        PlayerHero = new HeroBattleState(setup.Player.Hero); OpponentHero = new HeroBattleState(setup.Opponent.Hero);
        AddCards(setup.Player.Cards, SideId.Player); AddCards(setup.Opponent.Cards, SideId.Opponent); Cards.Sort(CompareCards);
    }
    public BattleTick Tick { get; set; } = BattleTick.Zero;
    public HeroBattleState PlayerHero { get; }
    public HeroBattleState OpponentHero { get; }
    public List<CardBattleState> Cards { get; } = [];
    public AbilityQueue Queue { get; } = new();
    public List<BattleEvent> Events { get; } = [];
    public List<PermanentChange> PermanentChanges { get; } = [];
    public HeroBattleState GetHero(SideId side) => side == SideId.Player ? PlayerHero : OpponentHero;
    private void AddCards(IReadOnlyList<CardBattleSetup> cards, SideId side) { foreach (var card in cards) Cards.Add(new(card, side)); }
    private static int CompareCards(CardBattleState left, CardBattleState right)
    {
        var side = left.Side.CompareTo(right.Side); if (side != 0) return side;
        var position = left.BoardStart.CompareTo(right.BoardStart);
        return position != 0 ? position : left.EntityId.Value.CompareTo(right.EntityId.Value);
    }
}
