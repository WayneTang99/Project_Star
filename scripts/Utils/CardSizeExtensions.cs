using Project_Star.Core.Types;

namespace Project_Star.Utils;

public static class CardSizeExtensions
{
	public static int GetCells(this CardSize size)
	{
		return size switch
		{
			CardSize.Small => 1,
			CardSize.Medium => 2,
			CardSize.Large => 3,
			_ => 0,
		};
	}
}