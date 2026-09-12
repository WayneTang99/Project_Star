using Project_Star.Core.Interfaces;

namespace Project_Star.Match.Events;

// 战斗胜利事件：玩家赢得一场战斗后触发，携带胜负结果与敌方剩余血量比例。
public partial class BattleWonEvent : MatchEventBase
{
	// 胜利方
	public ICombatant Winner { get; }

	// 敌方剩余生命比例（0~1，用于奖励按血量缩放）
	public float EnemyRemainingRatio { get; }

	// 是否为 PvP 战斗
	public bool IsPvP { get; }

	public BattleWonEvent(ICombatant winner, float enemyRemainingRatio, bool isPvP)
	{
		Winner = winner;
		EnemyRemainingRatio = enemyRemainingRatio;
		IsPvP = isPvP;
	}
}
