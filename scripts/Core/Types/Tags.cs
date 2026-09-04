using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Types;

// 游戏侧标签常量定义，卡牌/英雄挂载词条时引用。
public static class Tags
{
	// 尺寸标签
	public static readonly StringName Small = new("Small");
	public static readonly StringName Medium = new("Medium");
	public static readonly StringName Large = new("Large");

	// 根据 CardSize 枚举返回对应 Tag
	public static StringName FromSize(CardSize size) => size switch
	{
		CardSize.Small => Small,
		CardSize.Medium => Medium,
		CardSize.Large => Large,
		_ => Small,
	};
}
