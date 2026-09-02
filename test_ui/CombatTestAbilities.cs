using System;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Entities.Effects;
using Project_Star.Combat.Events;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.TestUI;

// 测试目标选取：按当前执行主体（Self）解析己方 / 敌方英雄（测试双方均可用能力）。
internal static class CombatTestTargeting
{
	// 当前主体的己方英雄
	public static ICombatant? MyHero(BattleContext ctx)
	{
		ICombatant? self = ctx.Self;
		if (self is null)
		{
			return null;
		}

		if (ReferenceEquals(self, ctx.FriendlyHero) || ReferenceEquals(self, ctx.EnemyHero))
		{
			return ReferenceEquals(self, ctx.FriendlyHero) ? ctx.FriendlyHero : ctx.EnemyHero;
		}

		foreach (ICombatant card in ctx.FriendlyCards)
		{
			if (ReferenceEquals(card, self))
			{
				return ctx.FriendlyHero;
			}
		}

		foreach (ICombatant card in ctx.EnemyCards)
		{
			if (ReferenceEquals(card, self))
			{
				return ctx.EnemyHero;
			}
		}

		return null;
	}

	// 当前主体的敌方英雄
	public static ICombatant? EnemyHeroOf(BattleContext ctx)
	{
		ICombatant? my = MyHero(ctx);
		if (my is null)
		{
			return ctx.EnemyHero;
		}

		return ReferenceEquals(my, ctx.FriendlyHero) ? ctx.EnemyHero : ctx.FriendlyHero;
	}
}

// 主动能力：对敌方英雄造成普通伤害。
internal sealed partial class CombatTestAttackAbility : AriaAbilityBase
{
	private readonly float _damage;

	public CombatTestAttackAbility(float cooldown, float damage)
	{
		Key = new StringName("Attack");
		DisplayName = "攻击";
		CooldownSeconds = cooldown;
		_damage = damage;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.EnemyHeroOf(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new DamageEffect { DamageAmount = _damage } ] } } ];
	}
}

// 主动能力：对敌方英雄造成穿透伤害（无视护甲）。
internal sealed partial class CombatTestPierceAbility : AriaAbilityBase
{
	private readonly float _damage;

	public CombatTestPierceAbility(float cooldown, float damage)
	{
		Key = new StringName("Pierce");
		DisplayName = "穿透射击";
		CooldownSeconds = cooldown;
		_damage = damage;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.EnemyHeroOf(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new PierceDamageEffect { DamageAmount = _damage } ] } } ];
	}
}

// 主动能力：对敌方英雄施加周期穿透伤害（辐射）。
internal sealed partial class CombatTestRadiationAbility : AriaAbilityBase
{
	private readonly float _damagePerTick;

	public CombatTestRadiationAbility(float cooldown, float damagePerTick)
	{
		Key = new StringName("Radiation");
		DisplayName = "辐射";
		CooldownSeconds = cooldown;
		_damagePerTick = damagePerTick;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.EnemyHeroOf(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new RadiationEffect { DamageAmount = _damagePerTick } ] } } ];
	}
}

// 主动能力：治疗己方英雄。
internal sealed partial class CombatTestHealAbility : AriaAbilityBase
{
	private readonly float _amount;

	public CombatTestHealAbility(float cooldown, float amount)
	{
		Key = new StringName("Heal");
		DisplayName = "治疗";
		CooldownSeconds = cooldown;
		_amount = amount;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.MyHero(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new HealEffect { HealAmount = _amount } ] } } ];
	}
}

// 被动能力（荆棘）：己方英雄受到伤害时，对敌方英雄反弹伤害。
internal sealed partial class CombatTestThornsAbility : AriaAbilityBase, IPassiveAbility
{
	private readonly float _reflect;

	// 响应事件类型：伤害结算
	public Type ReactEventType => typeof(DamageDealtEvent);

	public CombatTestThornsAbility(float reflect)
	{
		Key = new StringName("Thorns");
		DisplayName = "荆棘";
		_reflect = reflect;
	}

	public override bool CanActivate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		return ctx.CurrentEvent is DamageDealtEvent dmg
			&& dmg.Info.Target is not null
			&& ReferenceEquals(dmg.Info.Target, CombatTestTargeting.MyHero(ctx));
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.EnemyHeroOf(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new DamageEffect { DamageAmount = _reflect } ] } } ];
	}
}

// 被动能力（吸血）：己方英雄造成伤害时，治疗己方英雄。
internal sealed partial class CombatTestLifedrainAbility : AriaAbilityBase, IPassiveAbility
{
	private readonly float _drain;

	// 响应事件类型：伤害结算
	public Type ReactEventType => typeof(DamageDealtEvent);

	public CombatTestLifedrainAbility(float drain)
	{
		Key = new StringName("Lifedrain");
		DisplayName = "吸血";
		_drain = drain;
	}

	public override bool CanActivate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		return ctx.CurrentEvent is DamageDealtEvent dmg
			&& dmg.Info.Source is not null
			&& ReferenceEquals(dmg.Info.Source, CombatTestTargeting.MyHero(ctx));
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.MyHero(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new HealEffect { HealAmount = _drain } ] } } ];
	}
}

// 被动能力（濒死狂暴）：己方英雄生命首次跌破半血时治疗己方英雄。
internal sealed partial class CombatTestBerserkAbility : AriaAbilityBase, IPassiveAbility
{
	private readonly float _heal;

	// 响应事件类型：生命跌破半血
	public Type ReactEventType => typeof(HealthBelowHalfEvent);

	public CombatTestBerserkAbility(float heal)
	{
		Key = new StringName("Berserk");
		DisplayName = "濒死狂暴";
		_heal = heal;
	}

	public override bool CanActivate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		return ctx.CurrentEvent is HealthBelowHalfEvent evt
			&& ReferenceEquals(evt.Combatant, CombatTestTargeting.MyHero(ctx));
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.MyHero(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new HealEffect { HealAmount = _heal } ] } } ];
	}
}

// 被动能力（协同）：相邻卡牌发动能力时，对敌方英雄造成伤害。
internal sealed partial class CombatTestSynergyAbility : AriaAbilityBase, IPassiveAbility
{
	private readonly float _damage;

	// 响应事件类型：相邻卡牌能力发动
	public Type ReactEventType => typeof(AdjacentCardActivatedEvent);

	public CombatTestSynergyAbility(float damage)
	{
		Key = new StringName("Synergy");
		DisplayName = "协同";
		_damage = damage;
	}

	public override bool CanActivate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		if (ctx.CurrentEvent is not AdjacentCardActivatedEvent evt || ctx.Self is null)
		{
			return false;
		}

		foreach (ICombatant adjacent in evt.AdjacentCards)
		{
			if (ReferenceEquals(adjacent, ctx.Self))
			{
				return true;
			}
		}

		return false;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = CombatTestTargeting.EnemyHeroOf(ctx);
		if (target is null)
		{
			return [];
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new DamageEffect { DamageAmount = _damage } ] } } ];
	}
}