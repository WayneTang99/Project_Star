using Godot;

namespace Aria;

// 能力触发方式
public enum AbilityTriggerType
{
	// 主动（冷却就绪时自动发动）
	Active,
	// 被动（事件触发）
	Passive,
}

// 能力目标选择
public enum AbilityTargetType
{
	// 敌方英雄
	EnemyHero,
	// 己方英雄
	FriendlyHero,
	// 随机敌方卡牌
	RandomEnemyCard,
	// 所有敌方
	AllEnemies,
	// 自身
	Self,
	// 所有友方
	AllFriendlies,
}

// 数据驱动能力定义：不继承 AriaAbilityBase，而是持有配置数据 + 行为委托。
// 用于在运行时动态创建能力实例，无需为每种能力写 C# 子类。
[GlobalClass]
public partial class AbilityDefinition : Resource
{
	// 能力标识
	[Export] public StringName Key { get; set; } = new("Ability");

	// 展示名
	[Export] public string DisplayName { get; set; } = "Ability";

	// 触发方式
	[Export] public AbilityTriggerType TriggerType { get; set; } = AbilityTriggerType.Active;

	// 目标选择
	[Export] public AbilityTargetType TargetType { get; set; } = AbilityTargetType.EnemyHero;

	// 冷却秒数（主动能力用）
	[Export] public float CooldownSeconds { get; set; }

	// 激活条件（可为 null，表示无条件）
	public Condition? ActivationCondition { get; set; }

	// 效果列表（数据驱动效果定义）
	public Godot.Collections.Array<EffectDefinition> Effects { get; set; } = new();
}
