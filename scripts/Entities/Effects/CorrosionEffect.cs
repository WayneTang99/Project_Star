using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 腐蚀效果：即时施加，把腐蚀值加进目标属性；每帧由 CombatManager 按值结算伤害并线性衰减。
[GlobalClass]
public partial class CorrosionEffect : AriaEffectBase
{
	// 施加的腐蚀值（值 = 每秒伤害）
	[Export]
	public float DamageAmount { get; set; } = 5f;

	public CorrosionEffect()
	{
		Key = new StringName("Corrosion");
		DisplayName = "腐蚀";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.EnemyHero;
		if (target?.AttributeSet.GetAttribute(HeroAttributeSet.CORROSION) is not { } corrosion)
		{
			return;
		}

		corrosion.SetCurrentValue(corrosion.CurrentValue + DamageAmount);
	}
}