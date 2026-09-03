using System;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 通用被动伤害能力：响应指定事件类型，对敌方英雄造成可配置数值的伤害。
[GlobalClass]
public partial class PassiveDamageAbility : AriaAbilityBase, IPassiveAbility
{
	// 伤害数值
	[Export]
	public float DamageAmount { get; set; } = 10f;

	// 响应的事件类型
	[Export]
	public CombatEventType EventType { get; set; } = CombatEventType.BattleStart;

	public Type ReactEventType => EventType switch
	{
		CombatEventType.BattleStart => typeof(Combat.Events.BattleStartEvent),
		CombatEventType.AbilityActivated => typeof(Combat.Events.AbilityActivatedEvent),
		CombatEventType.DamageDealt => typeof(Combat.Events.DamageDealtEvent),
		CombatEventType.EffectApplied => typeof(Combat.Events.EffectAppliedEvent),
		CombatEventType.HealthBelowHalf => typeof(Combat.Events.HealthBelowHalfEvent),
		CombatEventType.NearDeath => typeof(Combat.Events.NearDeathEvent),
		_ => typeof(Combat.Events.BattleStartEvent),
	};

	public PassiveDamageAbility()
	{
		Key = new StringName("PassiveDamage");
		DisplayName = "被动伤害";
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = ctx.EnemyHero;
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new DamageEffect { DamageAmount = DamageAmount } ] } } ];
	}
}

// 战斗事件类型枚举（供 Export 下拉选择）
public enum CombatEventType
{
	BattleStart,
	AbilityActivated,
	DamageDealt,
	EffectApplied,
	HealthBelowHalf,
	NearDeath,
}
