using System.Collections.Generic;
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

	// 词条类型标签
	public static readonly StringName Weapon = new("Weapon");       // 武器
	public static readonly StringName Clothing = new("Clothing");   // 服饰
	public static readonly StringName Human = new("Human");         // 人类
	public static readonly StringName Mechanical = new("Mechanical"); // 机械
	public static readonly StringName Vehicle = new("Vehicle");     // 载具
	public static readonly StringName Consumable = new("Consumable"); // 消耗品

	// 标签 → 中文显示名映射
	private static readonly Dictionary<StringName, string> DisplayNames = new()
	{
		[Small] = "小型",
		[Medium] = "中型",
		[Large] = "大型",
		[Weapon] = "武器",
		[Clothing] = "服饰",
		[Human] = "人类",
		[Mechanical] = "机械",
		[Vehicle] = "载具",
		[Consumable] = "消耗品",
	};

	// 获取标签的中文显示名，未知标签返回原始 key
	public static string GetDisplayName(StringName tag) =>
		DisplayNames.TryGetValue(tag, out string? name) ? name : (string)tag;
}
