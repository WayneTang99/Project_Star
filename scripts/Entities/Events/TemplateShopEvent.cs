using Godot;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 模板商店事件：商店类示例事件。
public partial class TemplateShopEvent : ShopEventBase
{
	public TemplateShopEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Template_Shop"), "模板商店");
	}
}