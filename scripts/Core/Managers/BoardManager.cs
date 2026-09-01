using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Board;

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
}