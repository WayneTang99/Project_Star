using Project_Star.Core.Interfaces;

namespace Project_Star.Match.Events;

// 战斗胜利事件：玩家赢得一场战斗后触发。
public partial class BattleWonEvent : MatchEventBase
{
	// 胜利方
	public ICombatant Winner { get; }

	public BattleWonEvent(ICombatant winner)
	{
		Winner = winner;
	}
}
