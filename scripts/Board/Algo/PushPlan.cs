using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Board.Algo;

// 推挤方案：一次放置 / 移动的计算结果数据。
public sealed class PushPlan
{
	// 方案是否可行
	public bool IsFeasible { get; set; }

	// 推挤方向
	public PushDirection Direction { get; set; }

	// 推挤距离（格数）
	public int PushDistance { get; set; }

	// 受影响（被推挤）的卡牌
	public List<CardBase> AffectedCards { get; set; } = new();

	// 被拖拽的卡牌
	public CardBase DraggedCard { get; set; } = null!;

	// 目标起始格
	public int TargetCell { get; set; }

	// 执行后各卡牌的目标起始格
	public Dictionary<CardBase, int> ResultOffsets { get; set; } = new();
}