using System.Collections.Generic;
using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Board;
using Project_Star.Utils;

namespace Project_Star.Core.Managers;

[GlobalClass]
public partial class BoardManager : Node
{
	public const int BOARD_CAPACITY = 10;

	private readonly PushEvaluator _evaluator = new();

	private readonly PushExecutor _executor = new();

	public GameBoard Battlefield { get; } = new(BOARD_CAPACITY);

	public GameBoard Bench { get; } = new(BOARD_CAPACITY);

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

	public PushPlan PreviewMove(CardBase card, GameBoard targetBoard, int targetCell)
	{
		return _evaluator.Evaluate(targetBoard, card, targetCell);
	}

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

	public (GameBoard Board, int Cell)? AutoPlaceCard(CardBase card)
	{
		return TryAutoPlace(card, Battlefield) ?? TryAutoPlace(card, Bench);
	}

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

	private bool PlaceCardAt(GameBoard board, CardBase card, int cell)
	{
		GameBoard? source = GetBoardOf(card);
		if (source is not null && source != board)
		{
			source.RemoveCard(card);
		}

		return _executor.Apply(board, _evaluator.Evaluate(board, card, cell));
	}

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