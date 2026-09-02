using Godot;

namespace Aria;

// 效果基类：被能力触发的被动后果（静态修饰 / 周期 DoT-HoT），不触发能力。
[GlobalClass]
public abstract partial class AriaEffectBase : Resource
{
	// 效果标识（不可变）
	public StringName Key { get; protected set; } = new("Effect");

	// 效果展示名（不可变）
	public string DisplayName { get; protected set; } = "Effect";

	// 持续类型
	public AriaEffectDurationType DurationType { get; protected set; } = AriaEffectDurationType.Instant;

	// 持续秒数（HasDuration 用）
	public float DurationSeconds { get; protected set; }

	// 周期秒数（大于 0 表示周期 DoT/HoT，Instant 无效）
	public float PeriodSeconds { get; protected set; }

	// 属性修饰器列表（静态/持续修饰）
	public AriaAttributeModifier[] Modifiers { get; protected set; } = [];

	// 应用效果（写修饰器/挂载），供子类覆写
	public virtual void Apply(AriaContextBase ctx)
	{
	}

	// 移除效果（回滚），供子类覆写
	public virtual void Remove(AriaContextBase ctx)
	{
	}

	// 周期跳动，由管理器驱动，供子类覆写
	public virtual void Tick(float delta, AriaContextBase ctx)
	{
	}

	// 周期到期时产生的结算项；默认结算自身，自定义周期效果覆写（如返回内嵌即时效果）
	public virtual AriaEffectBase[] GetTickEffects(AriaContextBase ctx) => [this];
}