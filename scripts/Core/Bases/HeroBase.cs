using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Core.Bases;

// 英雄基类，持有一份独立的英雄属性集与标签集，初始属性由子类覆写实现差异。
[GlobalClass]
public partial class HeroBase : Node, ICombatant
{
	// 英雄属性集
	[Export]
	public HeroAttributeSet AttributeSet { get; set; } = new();

	// 标签集（词条）
	public AriaTagSet TagSet { get; set; } = new();

	// 能力（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaAbilityBase> Abilities { get; } = new();

	// 效果（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaEffectBase> Effects { get; } = new();

	// IAriaEntity 显式实现：以基类型暴露属性集
	AriaAttributeSet IAriaEntity.AttributeSet => AttributeSet;

	public override void _Ready()
	{
		base._Ready();
		ApplyInitialAttributes();
		// 同步 Health 上限：子类可能先设 Health 再设 MaxHealth，导致 Health 被 Clamp 到旧上限
		AttributeSet.Health.SetMaxValue(AttributeSet.MaxHealth.CurrentValue);
	}

	// 应用英雄初始属性，供子类覆写实现初始属性差异
	protected virtual void ApplyInitialAttributes()
	{
	}
}