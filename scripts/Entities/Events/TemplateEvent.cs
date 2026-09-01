using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 模板事件：通用示例事件。
public partial class TemplateEvent : EventBase
{
	public TemplateEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Template_Event"), "模板事件");
	}
}