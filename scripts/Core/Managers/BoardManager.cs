using System.Collections.Generic;
using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Board;
using Project_Star.Utils;

namespace Project_Star.Core.Managers;

// 棋盘管理器：持有战场区与备战区两个棋盘，编排放置 / 移动 / 推挤 / 跨区拖拽。
[GlobalClass]
public partial class BoardManager : Node
{
	// 每个棋盘的格子容量
	public const int BOARD_CAPACITY = 10;

	private readonly PushEvaluator _evaluator = new();

	private readonly PushExecutor _executor = new();

	// 战场区棋盘
	public GameBoard Battlefield { get; } = new(BOARD_CAPACITY);

	// 备战区棋盘
	public GameBoard Bench { get; } = new(BOARD_CAPACITY);

	// 查找卡牌所在的棋盘；不在任何棋盘上时返回 null
	public GameBoard? GetBoardOf(CardBase card)
	{
		if (Battlefield.ContainsCard(card))
		{
			return Battlefield;
		}

		if (Bench.ContainsCard(card))
		{
			return Bench;
		}

		return null;
	}

	// 只读预览一次放置 / 移动的推挤方案，不修改棋盘
	public PushPlan PreviewMove(CardBase card, GameBoard targetBoard, int targetCell)
	{
		return _evaluator.Evaluate(targetBoard, card, targetCell);
	}

	// 提交放置 / 移动；方案不可行返回 false。跨盘移动会先从来源盘移除
	public bool CommitMove(CardBase card, GameBoard targetBoard, int targetCell)
	{
		PushPlan plan = _evaluator.Evaluate(targetBoard, card, targetCell);
		if (!plan.IsFeasible)
		{
			return false;
		}

		GameBoard? source = GetBoardOf(card);
		if (source is not null && source != targetBoard)
		{
			source.RemoveCard(card);
		}

		return _executor.Apply(targetBoard, plan);
	}

	// 自动放置卡牌：先在战场区找空位，其次备战区；都放不下返回 null
	public (GameBoard Board, int Cell)? AutoPlaceCard(CardBase card)
	{
		return TryAutoPlace(card, Battlefield) ?? TryAutoPlace(card, Bench);
	}

	// 尝试在单个棋盘自动落位：先找空位，再尝试推挤方案
	private (GameBoard Board, int Cell)? TryAutoPlace(CardBase card, GameBoard board)
	{
		int cells = card.AttributeSet.Size.GetCells();
		List<BoardEntry> working = CopyEntriesWithout(board, card);

		for (int cell = 0; cell <= board.Capacity - cells; cell++)
		{
			if (BoardUtil.IsRangeFree(working, board.Capacity, cell, cells))
			{
				return PlaceCardAt(board, card, cell) ? (board, cell) : null;
			}
		}

		for (int cell = 0; cell <= board.Capacity - cells; cell++)
		{
			PushPlan plan = _evaluator.Evaluate(board, card, cell);
			if (plan.IsFeasible && _executor.Apply(board, plan))
			{
				return (board, plan.TargetCell);
			}
		}

		return null;
	}

	// 将卡牌落位到指定格，跨盘移动时先从来源盘移除
	private bool PlaceCardAt(GameBoard board, CardBase card, int cell)
	{
		GameBoard? source = GetBoardOf(card);
		if (source is not null && source != board)
		{
			source.RemoveCard(card);
		}

		return _executor.Apply(board, _evaluator.Evaluate(board, card, cell));
	}

	// 复制条目列表，排除被操作的卡牌（用于释放其原位）
	private static List<BoardEntry> CopyEntriesWithout(GameBoard board, CardBase card)
	{
		var copy = new List<BoardEntry>();
		foreach (BoardEntry entry in board.Entries)
		{
			if (entry.Card == card)
			{
				continue;
			}

			copy.Add(new BoardEntry(entry.Card, entry.Order, entry.StartCell) { CanPush = entry.CanPush });
		}

		return copy;
	}
}