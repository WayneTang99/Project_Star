using Aria;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.AttributeSets;

// 卡牌属性集：身份字段不可变，等级走 AriaAttributeData 流转。
[GlobalClass]
public partial class CardAttributeSet : AriaAttributeSet
{
	// 等级属性 key
	public const string LEVEL = "Level";

	// 卡牌标识 key（不可变）
	public StringName CardKey { get; }

	// 卡牌展示名（不可变）
	public string DisplayName { get; }

	// 阵营 key（不可变）
	public StringName FactionKey { get; }

	// 卡牌尺寸（不可变）
	public CardSize Size { get; }

	// 等级属性
	public AriaAttributeData Level => GetAttribute(LEVEL)!;

	// 构造：注入不可变身份字段并初始化等级属性
	public CardAttributeSet(StringName cardKey, string displayName, StringName factionKey, CardSize size)
	{
		CardKey = cardKey;
		DisplayName = displayName;
		FactionKey = factionKey;
		Size = size;
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
	}
}