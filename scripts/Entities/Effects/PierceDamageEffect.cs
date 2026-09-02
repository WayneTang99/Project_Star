using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Entities.Effects;

// 即时穿透伤害效果：应用一次立即结算，无视护甲直接扣除目标生命。
[GlobalClass]
public partial class PierceDamageEffect : AriaEffectBase, IDamageEffect
{
	// 伤害数值
	[Export]
	public float DamageAmount { get; set; } = 10f;

	// 是否穿透：是
	public bool IsPiercing => true;

	// 本次结算护甲吸收量：穿透恒为 0
	public float LastAbsorbedByArmor => 0f;

	public PierceDamageEffect()
	{
		Key = new StringName("PierceDamage");
		DisplayName = "穿透伤害";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.EnemyHero;
		if (target?.AttributeSet.GetAttribute(HeroAttributeSet.HEALTH) is not { } health)
		{
			return;
		}

		health.SetCurrentValue(health.CurrentValue - DamageAmount);
	}
}