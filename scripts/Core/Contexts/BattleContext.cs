using System.Collections.Generic;
using Aria;
using Project_Star.Core.Interfaces;
using Project_Star.Core.Types;

namespace Project_Star.Core.Bases;

// 战斗上下文：管理器调用能力/效果时构造并传入，供执行逻辑访问战场信息。
public partial class BattleContext : AriaContextBase
{
	// 来源
	public ICombatant? Source { get; set; }

	// 目标
	public ICombatant? Target { get; set; }

	// 触发方式
	public TriggerType TriggerType { get; set; }

	// 我方英雄（单个）
	public ICombatant? FriendlyHero { get; set; }

	// 我方卡牌
	public List<ICombatant> FriendlyCards { get; set; } = new();

	// 敌方英雄（单个）
	public ICombatant? EnemyHero { get; set; }

	// 敌方卡牌
	public List<ICombatant> EnemyCards { get; set; } = new();
}
