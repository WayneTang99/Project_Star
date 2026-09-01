using Project_Star.Core.Bases;

namespace Project_Star.Core.Board;

public sealed class BoardEntry
{
	public CardBase Card { get; }

	public int Order { get; set; }

	public int StartCell { get; set; }

	public bool CanPush { get; set; } = true;

	public BoardEntry(CardBase card, int order, int startCell)
	{
		Card = card;
		Order = order;
		StartCell = startCell;
	}
}