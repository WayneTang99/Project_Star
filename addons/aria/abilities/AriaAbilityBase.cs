using Godot;

namespace Aria;

// 能力基类：核心是执行逻辑；触发/冷却/编排由管理器统一负责。
[GlobalClass]
public abstract partial class AriaAbilityBase : Resource
{
	// 能力标识（不可变）
	public StringName Key { get; protected set; } = new("Ability");

	// 能力展示名（不可变）
	public string DisplayName { get; protected set; } = "Ability";

	// 冷却秒数（主动能力用）
	public float CooldownSeconds { get; protected set; }

	// 是否有冷却（CooldownSeconds 大于 0 即有）
	public bool HasCooldown => CooldownSeconds > 0f;

	// 能否发动，供子类覆写条件判断
	public virtual bool CanActivate(AriaContextBase ctx) => true;

	// 发动：返回本次产生的动作（目标 → 效果），供管理器收集后统一执行
	public virtual AriaAction[] Activate(AriaContextBase ctx) => [];
}