using Aria;
using Godot;

namespace Project_Star.Core.AttributeSets;

// 事件属性集：身份字段不可变，等级 / 轮次范围走 AriaAttributeData 流转。
[GlobalClass]
public partial class EventAttributeSet : AriaAttributeSet
{
	// 等级属性 key
	public const string LEVEL = "Level";
	// 最小轮次属性 key
	public const string MIN_ROUND = "MinRound";
	// 最大轮次属性 key
	public const string MAX_ROUND = "MaxRound";

	// 事件标识 key（不可变）
	public StringName EventKey { get; }

	// 事件展示名（不可变）
	public string EventDisplayName { get; }

	// 等级属性（1~5 级）
	public AriaAttributeData Level => GetAttribute(LEVEL)!;

	// 最小可排布轮次属性
	public AriaAttributeData MinRound => GetAttribute(MIN_ROUND)!;

	// 最大可排布轮次属性
	public AriaAttributeData MaxRound => GetAttribute(MAX_ROUND)!;

	// 构造：注入不可变身份字段并初始化等级与轮次范围属性
	public EventAttributeSet(StringName eventKey, string eventDisplayName, int minRound = 1, int maxRound = int.MaxValue, int level = 1)
	{
		EventKey = eventKey;
		EventDisplayName = eventDisplayName;
		AddAttribute(LEVEL, new AriaAttributeData(level, 1f, 5f));
		AddAttribute(MIN_ROUND, new AriaAttributeData(minRound, 1f, float.MaxValue));
		AddAttribute(MAX_ROUND, new AriaAttributeData(maxRound, 1f, float.MaxValue));
	}
}