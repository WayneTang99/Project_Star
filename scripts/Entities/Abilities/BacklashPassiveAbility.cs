using System;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Combat.Events;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 反噬被动能力：当敌方卡牌发动能力时，对敌方英雄造成伤害。
[GlobalClass]
public partial class BacklashPassiveAbility : AriaAbilityBase, IPassiveAbility
{
	// 伤害数值
	[Export]
	public float DamageAmount { get; set; } = 10f;

	public Type ReactEventType => typeof(AbilityActivatedEvent);

	public BacklashPassiveAbility()
	{
		Key = new StringName("Backlash");
		DisplayName = "反噬";
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;

		if (ctx.CurrentEvent is not AbilityActivatedEvent abilityEvent)
		{
			return [];
		}

		// 仅在敌方卡牌发动能力时触发
		if (abilityEvent.Combatant is null
			|| (!ctx.EnemyCards.Contains(abilityEvent.Combatant)
				&& abilityEvent.Combatant != ctx.EnemyHero))
		{
			return [];
		}

		ICombatant? target = ctx.EnemyHero;
		if (target is null)
		{
			return [];
		}

		return [new AriaAction { EffectsByTarget = { [target] = [new DamageEffect { DamageAmount = DamageAmount }] } }];
	}
}
