using System.Collections.Generic;
using Aria;
using Project_Star.Combat.Events;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Contexts;

// 战斗上下文：管理器调用能力/效果时构造并传入，供执行逻辑访问战场信息。
public partial class BattleContext : AriaContextBase
{
	// 来源
	public ICombatant? Source { get; set; }

	// 目标
	public ICombatant? Target { get; set; }

	// 当前执行主体（发动能力 / 持有被评估效果的实体，被动能力据此识别归属）
	public ICombatant? Self { get; set; }

	// 当前分发的事件（被动能力按此读取触发载荷）
	public CombatEventBase? CurrentEvent { get; set; }

	// 我方英雄（单个）
	public ICombatant? FriendlyHero { get; set; }

	// 我方卡牌
	public List<ICombatant> FriendlyCards { get; set; } = new();

	// 敌方英雄（单个）
	public ICombatant? EnemyHero { get; set; }

	// 敌方卡牌
	public List<ICombatant> EnemyCards { get; set; } = new();
}
