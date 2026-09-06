using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 超频效果：持续 Duration 秒，期间目标卡牌 OverclockDuration 属性 > 0，
// CombatManager 据此将 CD 读取速度 ×2。麻痹同时存在时抵消。
// 多个超频效果可叠加：Apply 累加时长，CombatManager 到期时按贡献扣减。
[GlobalClass]
public partial class OverclockEffect : AriaEffectBase
{
	// 本次施加的持续时长（秒）
	[Export]
	public float Duration { get; set; } = 5f;

	public OverclockEffect()
	{
		Key = new StringName("Overclock");
		DisplayName = "超频";
		DurationType = AriaEffectDurationType.HasDuration;
		DurationSeconds = Duration;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		if (ctx.Target?.AttributeSet is not CardAttributeSet card) return;
		// 累加时长，多个来源叠加
		card.OverclockDuration.SetCurrentValue(card.OverclockDuration.CurrentValue + Duration);
	}
}
