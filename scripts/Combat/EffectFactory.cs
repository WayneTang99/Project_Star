using Aria;
using Project_Star.Entities.Effects;

namespace Project_Star.Combat;

// 游戏侧效果工厂：从 EffectDefinition 创建具体效果实例。
// 将 Aria 的数据驱动定义映射到游戏的具体效果类，保持 Aria 插件游戏无关。
public static class EffectFactory
{
	public static AriaEffectBase Create(EffectDefinition def)
	{
		float value = def.Value?.Evaluate(null) ?? 0f;

		return def.Type switch
		{
			EffectType.Damage => new DamageEffect { DamageAmount = value },
			EffectType.PierceDamage => new PierceDamageEffect { DamageAmount = value },
			EffectType.Heal => new HealEffect { HealAmount = value },
			EffectType.Paralysis => new ParalysisEffect { Duration = def.DurationSeconds },
			EffectType.Overclock => new OverclockEffect { Duration = def.DurationSeconds },
			EffectType.Corrosion => new CorrosionEffect { DamageAmount = value },
			EffectType.Radiation => new RadiationEffect { DamageAmount = value },
			EffectType.Regeneration => new RegenerationEffect { HealAmount = value },
			EffectType.CooldownReduction => new CooldownReductionEffect { ReductionSeconds = value },
			EffectType.AttackBoost => new AttackBoostEffect { BoostAmount = value },
			_ => new DamageEffect { DamageAmount = value },
		};
	}
}
