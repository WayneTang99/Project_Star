using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 辐射效果：即时施加，把辐射值加进目标属性；每帧由 CombatManager 按值结算穿透伤害并指数衰减。
[GlobalClass]
public partial class RadiationEffect : AriaEffectBase
{
	// 施加的辐射值（值 = 每秒穿透伤害）
	[Export]
	public float DamageAmount { get; set; } = 5f;

	public RadiationEffect()
	{
		Key = new StringName("Radiation");
		DisplayName = "辐射";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.EnemyHero;
		if (target?.AttributeSet.GetAttribute(HeroAttributeSet.RADIATION) is not { } radiation)
		{
			return;
		}

		radiation.SetCurrentValue(radiation.CurrentValue + DamageAmount);
	}
}