using Godot;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 模板怪物事件：怪物类示例事件。
public partial class TemplateMonsterEvent : MonsterEventBase
{
	public TemplateMonsterEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Template_Monster"), "模板怪物");
	}
}