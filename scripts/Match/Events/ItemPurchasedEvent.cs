using Project_Star.Core.Bases;

namespace Project_Star.Match.Events;

// 购买物品事件：玩家在商店购买物品后触发。
public partial class ItemPurchasedEvent : MatchEventBase
{
	// 购买的卡牌
	public CardBase Card { get; }

	public ItemPurchasedEvent(CardBase card)
	{
		Card = card;
	}
}
