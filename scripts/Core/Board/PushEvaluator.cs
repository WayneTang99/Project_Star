using System;
using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Utils;

namespace Project_Star.Core.Board;

public sealed class PushEvaluator
{
	public PushPlan Evaluate(GameBoard board, CardBase card, int targetCell)
	{
		var plan = new PushPlan
		{
			DraggedCard = card,
			TargetCell = targetCell,
		};

		int cells = card.AttributeSet.Size.GetCells();

		if (targetCell < 0 || targetCell >= board.Capacity)
		{
			return plan;
		}

		int fitTarget = Math.Min(targetCell, board.Capacity - cells);

		if (board.GetStartCell(card) == fitTarget)
		{
			plan.IsFeasible = true;
			plan.TargetCell = fitTarget;
			plan.ResultOffsets = BuildOffsets(CreateWorkingCopy(board, card), card, fitTarget);
			return plan;
		}

		List<BoardEntry> working = CreateWorkingCopy(board, card);

		if (BoardUtil.IsRangeFree(working, board.Capacity, fitTarget, cells))
		{
			plan.IsFeasible = true;
			plan.TargetCell = fitTarget;
			plan.ResultOffsets = BuildOffsets(working, card, fitTarget);
			return plan;
		}

		foreach (BoardEntry blocker in BoardUtil.FindBlockers(working, fitTarget, cells))
		{
			if (!blocker.CanPush)
			{
				return plan;
			}
		}

		var rightWorking = CreateWorkingCopy(board, card);
		BoardUtil.PushSimulation right = BoardUtil.SimulatePush(rightWorking, board.Capacity, fitTarget, cells, PushDirection.Right);
		var leftWorking = CreateWorkingCopy(board, card);
		BoardUtil.PushSimulation left = BoardUtil.SimulatePush(leftWorking, board.Capacity, fitTarget, cells, PushDirection.Left);
		BoardUtil.PushSimulation? chosen = BoardUtil.SelectDirection(right, left);

		if (chosen is null)
		{
			return plan;
		}

		plan.IsFeasible = true;
		plan.TargetCell = fitTarget;
		plan.Direction = chosen.Direction;
		plan.PushDistance = chosen.Distance;
		plan.AffectedCards = chosen.AffectedCards;

		List<BoardEntry> chosenWorking = chosen.Direction == PushDirection.Right ? rightWorking : leftWorking;
		plan.ResultOffsets = BuildOffsets(chosenWorking, card, fitTarget);
		return plan;
	}

	private static List<BoardEntry> CreateWorkingCopy(GameBoard board, CardBase card)
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

	private static Dictionary<CardBase, int> BuildOffsets(List<BoardEntry> working, CardBase card, int targetCell)
	{
		var offsets = new Dictionary<CardBase, int>();
		foreach (BoardEntry entry in working)
		{
			offsets[entry.Card] = entry.StartCell;
		}

		offsets[card] = targetCell;
		return offsets;
	}
}