namespace Project_Star.Core.Types;

// 主游戏状态机，标识游戏所处的顶层流程阶段。
public enum GameState
{
	// 主菜单
	MainMenu,
	// 英雄选择
	HeroSelect,
	// 局内进行中
	InMatch,
	// 对局结算
	Result,
}

// 对局结束的原因，供结算界面与总线展示。
public enum MatchEndReason
{
	// 正常失败（如声望归零）
	Defeat,
	// 玩家主动认输
	Surrendered,
}