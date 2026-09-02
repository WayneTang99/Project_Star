using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 生命再生效果：即时施加，把再生量加进目标属性；每帧由 CombatManager 按值恢复生命并线性衰减。
[GlobalClass]
public partial class RegenerationEffect : AriaEffectBase
{
	// 施加的再生量（值 = 每秒恢复生命）
	[Export]
	public float HealAmount { get; set; } = 5f;

	public RegenerationEffect()
	{
		Key = new StringName("Regeneration");
		DisplayName = "生命再生";
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.FriendlyHero;
		if (target?.AttributeSet.GetAttribute(HeroAttributeSet.REGENERATION) is not { } regen)
		{
			return;
		}

		regen.SetCurrentValue(regen.CurrentValue + HealAmount);
	}
}