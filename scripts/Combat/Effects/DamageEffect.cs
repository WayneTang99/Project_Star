using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Effects;

// 即时伤害效果：应用一次立即结算，优先扣除目标护甲，剩余伤害再扣生命。
[GlobalClass]
public partial class DamageEffect : AriaEffectBase, IDamageEffect
{
	// 伤害数值
	[Export]
	public float DamageAmount { get; set; } = 10f;

	// 是否穿透：否
	public bool IsPiercing => false;

	// 本次结算护甲吸收量
	public float LastAbsorbedByArmor { get; private set; }

	public DamageEffect()
	{
		Key = new StringName("Damage");
		DisplayName = "伤害";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.EnemyHero;
		var attributeSet = target?.AttributeSet;
		if (attributeSet?.GetAttribute(HeroAttributeSet.ARMOR) is not { } armor
			|| attributeSet.GetAttribute(HeroAttributeSet.HEALTH) is not { } health)
		{
			LastAbsorbedByArmor = 0f;
			return;
		}

		float remaining = DamageAmount;
		float absorbed = Mathf.Min(armor.CurrentValue, remaining);
		armor.SetCurrentValue(armor.CurrentValue - absorbed);
		remaining -= absorbed;
		LastAbsorbedByArmor = absorbed;

		if (remaining > 0f)
		{
			health.SetCurrentValue(health.CurrentValue - remaining);
		}
	}
}