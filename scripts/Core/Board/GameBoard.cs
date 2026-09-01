using System;
using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Utils;

namespace Project_Star.Core.Board;

public sealed class GameBoard
{
	public const int DEFAULT_CAPACITY = 10;

	private readonly List<BoardEntry> _entries = new();

	public int Capacity { get; }

	public IReadOnlyList<BoardEntry> Entries => _entries;

	public event Action<CardBase>? CardPlacedEvent;

	public event Action<CardBase>? CardRemovedEvent;

	public event Action<CardBase, CardBase>? CardSwappedEvent;

	public event Action? LayoutChangedEvent;

	public GameBoard(int capacity = DEFAULT_CAPACITY)
	{
		Capacity = capacity;
	}

	public bool ContainsCard(CardBase card) => FindEntry(card) is not null;

	public BoardEntry? GetEntry(CardBase card) => FindEntry(card);

	public int? GetStartCell(CardBase card) => FindEntry(card)?.StartCell;

	public BoardEntry? GetEntryAt(int cell)
	{
		foreach (BoardEntry entry in _entries)
		{
			int start = entry.StartCell;
			int end = start + entry.Card.AttributeSet.Size.GetCells() - 1;
			if (cell >= start && cell <= end)
			{
				return entry;
			}
		}

		return null;
	}

	public bool IsRangeFree(int startCell, int count)
	{
		if (startCell < 0 || startCell + count > Capacity)
		{
			return false;
		}

		for (int cell = startCell; cell < startCell + count; cell++)
		{
			if (GetEntryAt(cell) is not null)
			{
				return false;
			}
		}

		return true;
	}

	public List<CardBase> GetCards()
	{
		var cards = new List<CardBase>();
		foreach (BoardEntry entry in _entries)
		{
			cards.Add(entry.Card);
		}

		return cards;
	}

	public List<(CardBase Card, int StartCell)> GetLayout()
	{
		var layout = new List<(CardBase, int)>();
		foreach (BoardEntry entry in _entries)
		{
			layout.Add((entry.Card, entry.StartCell));
		}

		return layout;
	}

	public bool RemoveCard(CardBase card)
	{
		BoardEntry? entry = FindEntry(card);
		if (entry is null)
		{
			return false;
		}

		_entries.Remove(entry);
		NormalizeOrders();
		CardRemovedEvent?.Invoke(card);
		LayoutChangedEvent?.Invoke();
		return true;
	}

	public bool SwapCards(CardBase a, CardBase b)
	{
		BoardEntry? entryA = FindEntry(a);
		BoardEntry? entryB = FindEntry(b);
		if (entryA is null || entryB is null || entryA == entryB)
		{
			return false;
		}

		(entryA.StartCell, entryB.StartCell) = (entryB.StartCell, entryA.StartCell);
		NormalizeOrders();
		CardSwappedEvent?.Invoke(a, b);
		LayoutChangedEvent?.Invoke();
		return true;
	}

	public void SetCanPush(CardBase card, bool canPush)
	{
		if (FindEntry(card) is { } entry)
		{
			entry.CanPush = canPush;
		}
	}

	internal void ApplyLayout(Dictionary<CardBase, int> offsets, CardBase placedCard)
	{
		bool wasOnBoard = placedCard is not null && ContainsCard(placedCard);

		for (int i = _entries.Count - 1; i >= 0; i--)
		{
			if (!offsets.ContainsKey(_entries[i].Card))
			{
				_entries.RemoveAt(i);
			}
		}

		foreach ((CardBase card, int startCell) in offsets)
		{
			if (FindEntry(card) is { } entry)
			{
				entry.StartCell = startCell;
			}
			else
			{
				_entries.Add(new BoardEntry(card, 0, startCell));
			}
		}

		NormalizeOrders();
		LayoutChangedEvent?.Invoke();
		if (placedCard is not null && !wasOnBoard)
		{
			CardPlacedEvent?.Invoke(placedCard);
		}
	}

	private BoardEntry? FindEntry(CardBase card)
	{
		foreach (BoardEntry entry in _entries)
		{
			if (entry.Card == card)
			{
				return entry;
			}
		}

		return null;
	}

	private void NormalizeOrders()
	{
		_entries.Sort((a, b) => a.StartCell.CompareTo(b.StartCell));
		for (int i = 0; i < _entries.Count; i++)
		{
			_entries[i].Order = i;
		}
	}
}