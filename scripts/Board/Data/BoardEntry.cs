using Project_Star.Core.Bases;

namespace Project_Star.Board.Data;

// 棋盘条目：卡牌在棋盘上的一份位置数据。
public sealed class BoardEntry
{
	// 卡牌引用
	public CardBase Card { get; }

	// 左→右顺序（归一化，从 0 起）
	public int Order { get; set; }

	// 起始格索引
	public int StartCell { get; set; }

	// 是否可被推挤
	public bool CanPush { get; set; } = true;

	// 构造：指定卡牌、顺序与起始格
	public BoardEntry(CardBase card, int order, int startCell)
	{
		Card = card;
		Order = order;
		StartCell = startCell;
	}
}