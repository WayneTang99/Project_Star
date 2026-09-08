using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// PvP 事件：异步玩家对战事件，末回合固定生成；对战对手由后续接线任务生成。
public partial class PvPEvent : EventBase
{
	public PvPEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("PvP_Event"), "玩家对战");
	}
}