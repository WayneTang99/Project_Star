using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 攻击能力：对敌方英雄（EnemyHero）造成伤害（优先扣除护甲，剩余扣生命）。
// 伤害值优先从持有者卡牌的 ATTACK_POWER 属性读取，无属性时回退到 DamageAmount。
[GlobalClass]
public partial class AttackAbility : AriaAbilityBase
{
	// 伤害数值（回退值，当持有者无 ATTACK_POWER 属性时使用）
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

		float damage = DamageAmount;
		if (Owner is CardBase card && card.AttributeSet.GetAttribute(CardAttributeSet.ATTACK_POWER) is { } attackPower)
		{
			damage = attackPower.CurrentValue;
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new DamageEffect { DamageAmount = damage } ] } } ];
	}
}
