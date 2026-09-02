using System.Collections.Generic;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 相邻卡牌能力发动事件：某卡牌发动能力时，携带与其相邻的卡牌列表。
public class AdjacentCardActivatedEvent : CombatEventBase
{
	// 发动能力的卡牌
	public ICombatant Activator { get; }

	// 与发动者相邻的卡牌（由战斗入口按棋盘布局解析）
	public IReadOnlyList<ICombatant> AdjacentCards { get; }

	public AdjacentCardActivatedEvent(ICombatant activator, IReadOnlyList<ICombatant> adjacentCards)
	{
		Activator = activator;
		AdjacentCards = adjacentCards;
	}
}