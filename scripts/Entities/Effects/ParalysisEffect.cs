using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 麻痹效果：持续 Duration 秒，期间目标卡牌 ParalysisDuration 属性 > 0，
// CombatManager 据此将 CD 读取速度 ×0.5。超频同时存在时抵消。
// 多个麻痹效果可叠加：Apply 累加时长，CombatManager 到期时按贡献扣减。
[GlobalClass]
public partial class ParalysisEffect : AriaEffectBase
{
	// 本次施加的持续时长（秒）
	[Export]
	public float Duration { get; set; } = 5f;

	public ParalysisEffect()
	{
		Key = new StringName("Paralysis");
		DisplayName = "麻痹";
		DurationType = AriaEffectDurationType.HasDuration;
		DurationSeconds = Duration;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		if (ctx.Target?.AttributeSet is not CardAttributeSet card) return;
		// 累加时长，多个来源叠加
		card.ParalysisDuration.SetCurrentValue(card.ParalysisDuration.CurrentValue + Duration);
	}
}
