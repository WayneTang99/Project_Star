using System;
using Aria;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 全局战斗事件总线（观察者通道）：由结算器在结算后广播，供 UI/统计/管理器订阅。
// 被动连锁不经过总线（需同步入队），由 CombatManager 内部索引处理。
public static class CombatEventBus
{
	// 伤害结算事件
	public static event Action<DamageInfo>? DamageDealt;

	// 能力发动事件
	public static event Action<ICombatant?, AriaAbilityBase>? AbilityActivated;

	// 效果结算事件
	public static event Action<ICombatant?, AriaEffectBase>? EffectApplied;

	// 生命值首次跌破半血事件
	public static event Action<ICombatant>? HealthBelowHalf;

	// 濒临死亡事件（生命值首次跌破濒死阈值）
	public static event Action<ICombatant>? NearDeath;

	// 广播伤害结算
	public static void RaiseDamageDealt(DamageInfo info) => DamageDealt?.Invoke(info);

	// 广播能力发动
	public static void RaiseAbilityActivated(ICombatant? combatant, AriaAbilityBase ability) => AbilityActivated?.Invoke(combatant, ability);

	// 广播效果结算
	public static void RaiseEffectApplied(ICombatant? target, AriaEffectBase effect) => EffectApplied?.Invoke(target, effect);

	// 广播生命值跌破半血
	public static void RaiseHealthBelowHalf(ICombatant combatant) => HealthBelowHalf?.Invoke(combatant);

	// 广播濒临死亡
	public static void RaiseNearDeath(ICombatant combatant) => NearDeath?.Invoke(combatant);
}