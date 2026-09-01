using System;
using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Utils;

namespace Project_Star.Core.Board;

// 棋盘推挤纯算法：阻挡查找、左右推挤逐格模拟、方向择优。
public static class BoardUtil
{
	// 单次推挤模拟的结果
	public sealed record PushSimulation(bool IsFeasible, PushDirection Direction, int Distance, List<CardBase> AffectedCards);

	// 指定区间（从 startCell 起 count 格）在容量内是否为空闲
	public static bool IsRangeFree(IReadOnlyList<BoardEntry> entries, int capacity, int startCell, int count)
	{
		if (startCell < 0 || startCell + count > capacity)
		{
			return false;
		}

		for (int cell = startCell; cell < startCell + count; cell++)
		{
			if (EntryAt(entries, cell) is not null)
			{
				return false;
			}
		}

		return true;
	}

	// 找出与目标区间相交的所有阻挡卡牌条目
	public static List<BoardEntry> FindBlockers(IReadOnlyList<BoardEntry> entries, int targetCell, int count)
	{
		var blockers = new List<BoardEntry>();
		foreach (BoardEntry entry in entries)
		{
			int start = entry.StartCell;
			int end = start + entry.Card.AttributeSet.Size.GetCells() - 1;
			if (end >= targetCell && start < targetCell + count)
			{
				blockers.Add(entry);
			}
		}

		return blockers;
	}

	// 向指定方向逐格推挤模拟；成功返回可行结果，被边界阻挡返回不可行
	public static PushSimulation SimulatePush(List<BoardEntry> working, int capacity, int targetCell, int count, PushDirection direction)
	{
		int distance = 0;
		var affected = new List<CardBase>();

		while (!IsRangeFree(working, capacity, targetCell, count))
		{
			List<BoardEntry> block = direction == PushDirection.Right
				? FindRightBlock(working, targetCell, count)
				: FindLeftBlock(working, targetCell, count);

			if (block.Count == 0)
			{
				return new PushSimulation(false, direction, distance, affected);
			}

			bool moved = direction == PushDirection.Right
				? TryShiftRight(working, block, capacity)
				: TryShiftLeft(working, block);

			if (!moved)
			{
				return new PushSimulation(false, direction, distance, affected);
			}

			distance++;
			foreach (BoardEntry entry in block)
			{
				if (!affected.Contains(entry.Card))
				{
					affected.Add(entry.Card);
				}
			}
		}

		return new PushSimulation(true, direction, distance, affected);
	}

	// 择优选择推挤方向：可行者优先，其次距离短，再次受影响卡牌少，平局取 Right
	public static PushSimulation? SelectDirection(PushSimulation right, PushSimulation left)
	{
		if (right.IsFeasible != left.IsFeasible)
		{
			return right.IsFeasible ? right : left;
		}

		if (!right.IsFeasible)
		{
			return null;
		}

		if (right.Distance != left.Distance)
		{
			return right.Distance < left.Distance ? right : left;
		}

		if (right.AffectedCards.Count != left.AffectedCards.Count)
		{
			return right.AffectedCards.Count < left.AffectedCards.Count ? right : left;
		}

		return right;
	}

	// 查找向右推挤时需整体移动的连续卡牌块（从最左阻挡卡开始向右延展）
	private static List<BoardEntry> FindRightBlock(IReadOnlyList<BoardEntry> entries, int targetCell, int count)
	{
		int left = int.MaxValue;
		foreach (BoardEntry blocker in FindBlockers(entries, targetCell, count))
		{
			left = Math.Min(left, blocker.StartCell);
		}

		var block = new List<BoardEntry>();
		if (left == int.MaxValue)
		{
			return block;
		}

		int cursor = left;
		while (true)
		{
			BoardEntry? entry = EntryAt(entries, cursor);
			if (entry is null)
			{
				break;
			}

			block.Add(entry);
			cursor = entry.StartCell + entry.Card.AttributeSet.Size.GetCells();
		}

		return block;
	}

	// 查找向左推挤时需整体移动的连续卡牌块（从最右阻挡卡开始向左延展）
	private static List<BoardEntry> FindLeftBlock(IReadOnlyList<BoardEntry> entries, int targetCell, int count)
	{
		int right = -1;
		foreach (BoardEntry blocker in FindBlockers(entries, targetCell, count))
		{
			right = Math.Max(right, blocker.StartCell + blocker.Card.AttributeSet.Size.GetCells() - 1);
		}

		var block = new List<BoardEntry>();
		if (right < 0)
		{
			return block;
		}

		int cursor = right;
		while (true)
		{
			BoardEntry? entry = EntryAt(entries, cursor);
			if (entry is null)
			{
				break;
			}

			block.Add(entry);
			cursor = entry.StartCell - 1;
		}

		return block;
	}

	// 尝试将卡牌块向右移动一格；越界返回 false
	private static bool TryShiftRight(List<BoardEntry> working, List<BoardEntry> block, int capacity)
	{
		int x = int.MinValue;
		foreach (BoardEntry entry in block)
		{
			x = Math.Max(x, entry.StartCell + entry.Card.AttributeSet.Size.GetCells() - 1);
		}

		while (x + 1 < capacity)
		{
			BoardEntry? next = EntryAt(working, x + 1);
			if (next is null)
			{
				break;
			}

			if (!block.Contains(next))
			{
				block.Add(next);
			}

			x = Math.Max(x, next.StartCell + next.Card.AttributeSet.Size.GetCells() - 1);
		}

		if (x + 1 >= capacity)
		{
			return false;
		}

		foreach (BoardEntry entry in block)
		{
			entry.StartCell += 1;
		}

		return true;
	}

	// 尝试将卡牌块向左移动一格；越界返回 false
	private static bool TryShiftLeft(List<BoardEntry> working, List<BoardEntry> block)
	{
		int l = int.MaxValue;
		foreach (BoardEntry entry in block)
		{
			l = Math.Min(l, entry.StartCell);
		}

		while (l - 1 >= 0)
		{
			BoardEntry? prev = EntryAt(working, l - 1);
			if (prev is null)
			{
				break;
			}

			if (!block.Contains(prev))
			{
				block.Add(prev);
			}

			l = Math.Min(l, prev.StartCell);
		}

		if (l - 1 < 0)
		{
			return false;
		}

		foreach (BoardEntry entry in block)
		{
			entry.StartCell -= 1;
		}

		return true;
	}

	// 返回占用指定格的条目；该格空闲返回 null
	private static BoardEntry? EntryAt(IReadOnlyList<BoardEntry> entries, int cell)
	{
		foreach (BoardEntry entry in entries)
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
}