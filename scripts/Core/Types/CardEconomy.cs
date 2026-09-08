namespace Project_Star.Core.Types;

// 卡牌经济规则：价值公式与交易倍率的唯一出处。
// 初始价值 = 价值系数 * 等级 * 型号系数（小 1 / 中 2 / 大 3）。
public static class CardEconomy
{
	// 普通卡价值系数
	public const float STANDARD_VALUE_SCALE = 2f;

	// 获得后现值系数：卡牌加入玩家卡池（获得）即按初始价值折半存入现值，
	// 与获得途径无关，供后续增值/减值事件的叠加逻辑在同一基线上进行
	public const float OBTAINED_VALUE_RATIO = 0.5f;

	// 型号系数：Small=1 / Medium=2 / Large=3
	public static int SizeIndex(CardSize size) => size switch
	{
		CardSize.Small => 1,
		CardSize.Medium => 2,
		CardSize.Large => 3,
		_ => 0,
	};

	// 计算初始价值 = 价值系数 * 等级 * 型号系数
	public static float InitialValue(float valueScale, int level, CardSize size)
		=> valueScale * level * SizeIndex(size);
}
