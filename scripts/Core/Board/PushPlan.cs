using System.Collections.Generic;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Core.Board;

public sealed class PushPlan
{
	public bool IsFeasible { get; set; }

	public PushDirection Direction { get; set; }

	public int PushDistance { get; set; }

	public List<CardBase> AffectedCards { get; set; } = new();

	public CardBase DraggedCard { get; set; } = null!;

	public int TargetCell { get; set; }

	public Dictionary<CardBase, int> ResultOffsets { get; set; } = new();
}