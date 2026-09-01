namespace Project_Star.Core.Types;

// 战斗触发方式：None=主动（冷却/手动发动）；其余=被动（事件触发）。
public enum TriggerType
{
	// 主动（冷却/手动发动）
	None,
	// 战斗开始
	BattleStart,
	// 某卡牌发动
	CardActivated,
	// 生命值过半
	HealthBelowHalf,
}
