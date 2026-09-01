using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 商店事件：商店对战事件，直接引用商人实体（商人继承 MerchantBase，与英雄同样持有属性集与卡牌）。
public partial class ShopEvent : EventBase
{
	// 商人实体
	[Export]
	public MerchantBase Merchant { get; set; } = null!;

	public ShopEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Shop_Event"), "商店事件");
	}
}
