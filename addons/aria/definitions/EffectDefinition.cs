using Godot;

namespace Aria;

// 效果类型枚举
public enum EffectType
{
	Damage,
	Heal,
	Paralysis,
	Overclock,
	Corrosion,
	Radiation,
	Regeneration,
	CooldownReduction,
	AttackBoost,
	PierceDamage,
}

// 效果持续类型
public enum EffectDurationType
{
	Instant,
	HasDuration,
	Permanent,
}

// 数据驱动效果定义：持有效果类型、数值表达式、持续时间等配置。
[GlobalClass]
public partial class EffectDefinition : Resource
{
	// 效果类型
	[Export] public EffectType Type { get; set; } = EffectType.Damage;

	// 数值表达式（动态计算：伤害量、治疗量等）
	public ValueExpression? Value { get; set; }

	// 持续类型
	[Export] public EffectDurationType DurationType { get; set; } = EffectDurationType.Instant;

	// 持续时间秒数（HasDuration 时使用）
	[Export] public float DurationSeconds { get; set; }

	// 周期秒数（HasDuration + 周期性效果时使用，0 = 无周期）
	[Export] public float PeriodSeconds { get; set; }

	// 是否穿透护甲（仅 Damage/PierceDamage 有效）
	[Export] public bool IsPiercing { get; set; }

	// 应用条件（可为 null，表示无条件应用）
	public Condition? ApplyCondition { get; set; }
}
