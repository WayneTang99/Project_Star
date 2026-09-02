using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Entities.Effects;
using Project_Star.Core.Interfaces;

namespace Project_Star.Entities.Abilities;

// 攻击能力：对敌方英雄（EnemyHero）造成伤害（优先扣除护甲，剩余扣生命）。
[GlobalClass]
public partial class AttackAbility : AriaAbilityBase
{
	// 伤害数值
	[Export]
	public float DamageAmount { get; set; } = 10f;

	public AttackAbility()
	{
		Key = new StringName("Attack");
		DisplayName = "攻击";
		CooldownSeconds = 1f;
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