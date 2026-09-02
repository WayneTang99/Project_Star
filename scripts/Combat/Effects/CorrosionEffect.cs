using Aria;
using Godot;

namespace Project_Star.Combat.Effects;

// 腐蚀效果：周期伤害，每次周期结算以内嵌伤害效果入队，由管理器统一结算。
[GlobalClass]
public partial class CorrosionEffect : AriaEffectBase
{
	// 每周期伤害数值
	[Export]
	public float DamageAmount { get; set; } = 5f;

	// 内嵌伤害效果（每周期结算一次）
	[Export]
	public DamageEffect Damage { get; set; }

	public CorrosionEffect()
	{
		Key = new StringName("Corrosion");
		DisplayName = "腐蚀";
		DurationType = AriaEffectDurationType.HasDuration;
		DurationSeconds = 5f;
		PeriodSeconds = 1f;
		Damage = new DamageEffect();
	}

	// 周期到期：以伤害效果作为本次结算项
	public override AriaEffectBase[] GetTickEffects(AriaContextBase ctx)
	{
		Damage.DamageAmount = DamageAmount;
		return [Damage];
	}
}