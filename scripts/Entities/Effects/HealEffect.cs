using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Entities.Effects;

// 即时治疗效果：应用一次立即恢复目标生命（不超过最大生命）；同时削减目标腐蚀 / 辐射（净化）。
[GlobalClass]
public partial class HealEffect : AriaEffectBase, IHealEffect
{
	// 治疗数值
	[Export]
	public float HealAmount { get; set; } = 10f;

	public HealEffect()
	{
		Key = new StringName("Heal");
		DisplayName = "治疗";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.FriendlyHero;
		if (target?.AttributeSet.GetAttribute(HeroAttributeSet.HEALTH) is not { } health)
		{
			return;
		}

		health.SetCurrentValue(health.CurrentValue + HealAmount);
	}
}