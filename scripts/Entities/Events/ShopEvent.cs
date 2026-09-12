using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 商店事件：商店事件，玩家可在此进行卡牌交易。
public partial class ShopEvent : EventBase
{
	public ShopEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Shop_Event"), "商店事件");
	}
}
