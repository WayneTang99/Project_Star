namespace Project_Star.Core.Board;

public sealed class PushExecutor
{
	public bool Apply(GameBoard board, PushPlan plan)
	{
		if (plan is null || !plan.IsFeasible)
		{
			return false;
		}

		board.ApplyLayout(plan.ResultOffsets, plan.DraggedCard);
		return true;
	}
}