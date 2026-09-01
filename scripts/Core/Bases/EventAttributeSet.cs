using Aria;
using Godot;

namespace Project_Star.Core.Bases;

// 事件属性集：身份字段不可变。
[GlobalClass]
public partial class EventAttributeSet : AriaAttributeSet
{
	// 事件标识 key（不可变）
	public StringName EventKey { get; }

	// 事件展示名（不可变）
	public string EventDisplayName { get; }

	// 构造：注入不可变身份字段
	public EventAttributeSet(StringName eventKey, string eventDisplayName)
	{
		EventKey = eventKey;
		EventDisplayName = eventDisplayName;
	}
}