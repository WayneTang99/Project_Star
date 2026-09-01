using Project_Star.Core.Types;

namespace Project_Star.Utils;

// CardSize 尺寸换算工具。
public static class CardSizeExtensions
{
	// 获取卡牌尺寸占用的棋盘格数
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