using System;
using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Utils;

namespace Project_Star.Core.Board;

// 棋盘数据管理：只存卡牌引用 + Order + StartCell，不存渲染坐标。
public sealed class GameBoard
{
	// 默认棋盘容量
	public const int DEFAULT_CAPACITY = 10;

	private readonly List<BoardEntry> _entries = new();

	// 棋盘容量（格数）
	public int Capacity { get; }

	// 全部卡牌条目（只读，按 StartCell 排序）
	public IReadOnlyList<BoardEntry> Entries => _entries;

	// 卡牌放置事件
	public event Action<CardBase>? CardPlacedEvent;

	// 卡牌移除事件
	public event Action<CardBase>? CardRemovedEvent;

	// 卡牌交换事件，参数为参与交换的两张卡牌
	public event Action<CardBase, CardBase>? CardSwappedEvent;

	// 布局变更事件
	public event Action? LayoutChangedEvent;

	// 构造，可指定棋盘容量
	public GameBoard(int capacity = DEFAULT_CAPACITY)
	{
		Capacity = capacity;
	}

	// 棋盘上是否包含指定卡牌
	public bool ContainsCard(CardBase card) => FindEntry(card) is not null;

	// 获取卡牌对应条目；不存在返回 null
	public BoardEntry? GetEntry(CardBase card) => FindEntry(card);

	// 获取卡牌起始格；不在棋盘上返回 null
	public int? GetStartCell(CardBase card) => FindEntry(card)?.StartCell;

	// 获取占用指定格的条目；该格空闲返回 null
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

	// 指定区间（从 startCell 起 count 格）是否为空闲且不越界
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

	// 获取棋盘上全部卡牌
	public List<CardBase> GetCards()
	{
		var cards = new List<CardBase>();
		foreach (BoardEntry entry in _entries)
		{
			cards.Add(entry.Card);
		}

		return cards;
	}

	// 获取布局快照（卡牌 + 起始格），UI 据此换算像素坐标
	public List<(CardBase Card, int StartCell)> GetLayout()
	{
		var layout = new List<(CardBase, int)>();
		foreach (BoardEntry entry in _entries)
		{
			layout.Add((entry.Card, entry.StartCell));
		}

		return layout;
	}

	// 移除卡牌并归一 order；不在棋盘上返回 false
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

	// 交换两张卡牌的起始格并归一 order；失败返回 false
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

	// 设置卡牌是否可被推挤
	public void SetCanPush(CardBase card, bool canPush)
	{
		if (FindEntry(card) is { } entry)
		{
			entry.CanPush = canPush;
		}
	}

	// 按偏移字典落位（供推挤执行器调用）：新增 / 移除 / 更新 StartCell 并归一 order
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

	// 查找卡牌对应条目
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

	// 按 StartCell 升序排序并重写 Order
	private void NormalizeOrders()
	{
		_entries.Sort((a, b) => a.StartCell.CompareTo(b.StartCell));
		for (int i = 0; i < _entries.Count; i++)
		{
			_entries[i].Order = i;
		}
	}
}