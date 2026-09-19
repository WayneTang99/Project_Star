using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Match;

/// <summary>The aggregate root and sole mutable state owner for one match.</summary>
public sealed class MatchSession
{
    public MatchSession(ulong seed, int startingWealth = 0, int boardCapacity = BoardState.DefaultCapacity, int startingReputation = 10)
    {
        if (startingWealth < 0 || startingReputation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startingWealth));
        }

        Id = Guid.NewGuid();
        Progress = new MatchProgress();
        Player = new PlayerState(startingWealth, startingReputation);
        Board = new BoardState(boardCapacity);
        EncounterSchedule = new EncounterScheduleState();
        Random = new MatchRandomState(seed);
    }

    public Guid Id { get; }

    public MatchProgress Progress { get; }

    public PlayerState Player { get; }

    public BoardState Board { get; }

    public EncounterScheduleState EncounterSchedule { get; }

    public MatchRandomState Random { get; }

    public List<IMatchEvent> Events { get; } = [];

    public MatchStatus Status { get; internal set; } = MatchStatus.InProgress;

    public MatchSummary? Summary { get; internal set; }
}

public sealed class MatchProgress
{
    public int Round { get; internal set; } = 1;

    public int Turn { get; internal set; } = 1;

    public int PvpWins { get; internal set; }
}

public sealed class PlayerState
{
    public PlayerState(int startingWealth, int startingReputation)
    {
        Resources.SetBaseValue(GameAttributeKeys.Wealth, startingWealth);
        Resources.SetBaseValue(GameAttributeKeys.Reputation, startingReputation);
    }

    public HeroInstance? Hero { get; private set; }

    public CardInventory Inventory { get; } = new();

    public ModifiableAttributeSet Resources { get; } = new();

    public int Wealth => Resources.GetBaseValue(GameAttributeKeys.Wealth);

    public int Reputation => Resources.GetBaseValue(GameAttributeKeys.Reputation);

    public void SelectHero(HeroInstance hero) => Hero = hero ?? throw new ArgumentNullException(nameof(hero));

    internal bool TrySpendWealth(int amount)
    {
        if (amount < 0 || Wealth < amount)
        {
            return false;
        }

        Resources.SetBaseValue(GameAttributeKeys.Wealth, Wealth - amount);
        return true;
    }

    internal void AddWealth(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Resources.SetBaseValue(GameAttributeKeys.Wealth, checked(Wealth + amount));
    }

    internal void LoseReputation(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Resources.SetBaseValue(GameAttributeKeys.Reputation, Reputation - amount);
    }
}

public sealed class CardInventory
{
    private readonly List<CardInstance> _cards = [];

    public IReadOnlyList<CardInstance> Cards => _cards;

    public void Add(CardInstance card) => _cards.Add(card ?? throw new ArgumentNullException(nameof(card)));

    public CardInstance? Find(EntityId id)
    {
        foreach (var card in _cards)
        {
            if (card.Id == id)
            {
                return card;
            }
        }

        return null;
    }

    internal bool Remove(EntityId id)
    {
        var card = Find(id);
        return card is not null && _cards.Remove(card);
    }
}

public sealed class EncounterScheduleState
{
    private readonly HashSet<StringName> _seenKeys = [];
    private readonly List<EncounterChoice> _currentChoices = [];

    public IReadOnlySet<StringName> SeenKeys => _seenKeys;

    public IReadOnlyList<EncounterChoice> CurrentChoices => _currentChoices;

    internal void SetChoices(IEnumerable<EncounterChoice> choices)
    {
        _currentChoices.Clear();
        foreach (var choice in choices)
        {
            _currentChoices.Add(choice);
            _seenKeys.Add(choice.Key);
        }
    }

    internal void ClearChoices() => _currentChoices.Clear();
}

public sealed class MatchRandomState
{
    public MatchRandomState(ulong seed)
    {
        Seed = seed;
        State = seed;
    }

    public ulong Seed { get; }

    public ulong State { get; internal set; }
}
