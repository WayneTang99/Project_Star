using Project_Star.Board.Data;

namespace Project_Star.Board.Algo;

// 推挤执行器：将可行的 PushPlan 写回棋盘。
public sealed class PushExecutor
{
	// 执行推挤方案；方案不可行返回 false
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