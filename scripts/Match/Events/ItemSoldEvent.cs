using Project_Star.Core.Bases;

namespace Project_Star.Match.Events;

// 出售物品事件：玩家在商店出售物品后触发。
public partial class ItemSoldEvent : MatchEventBase
{
	// 出售的卡牌
	public CardBase Card { get; }

	public ItemSoldEvent(CardBase card)
	{
		Card = card;
	}
}
