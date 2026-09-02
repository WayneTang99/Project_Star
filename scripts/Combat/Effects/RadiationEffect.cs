using Aria;
using Godot;

namespace Project_Star.Combat.Effects;

// 辐射效果：周期穿透伤害，每次周期结算以内嵌穿透伤害效果入队，由管理器统一结算。
[GlobalClass]
public partial class RadiationEffect : AriaEffectBase
{
	// 每周期伤害数值
	[Export]
	public float DamageAmount { get; set; } = 5f;

	// 内嵌穿透伤害效果（每周期结算一次）
	[Export]
	public PierceDamageEffect PierceDamage { get; set; }

	public RadiationEffect()
	{
		Key = new StringName("Radiation");
		DisplayName = "辐射";
		DurationType = AriaEffectDurationType.HasDuration;
		DurationSeconds = 5f;
		PeriodSeconds = 1f;
		PierceDamage = new PierceDamageEffect();
	}

	// 周期到期：以穿透伤害效果作为本次结算项
	public override AriaEffectBase[] GetTickEffects(AriaContextBase ctx)
	{
		PierceDamage.DamageAmount = DamageAmount;
		return [PierceDamage];
	}
}