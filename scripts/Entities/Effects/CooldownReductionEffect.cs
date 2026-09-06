using Aria;
using Godot;
using Project_Star.Combat.Contexts;

namespace Project_Star.Entities.Effects;

// 充能效果：立即减少目标所有能力的冷却秒数。由 CombatManager 结算时识别并执行。
[GlobalClass]
public partial class CooldownReductionEffect : AriaEffectBase
{
	// 减少的冷却秒数
	[Export]
	public float ReductionSeconds { get; set; } = 3f;

	public CooldownReductionEffect()
	{
		Key = new StringName("CooldownReduction");
		DisplayName = "充能";
		DurationType = AriaEffectDurationType.Instant;
	}
}
