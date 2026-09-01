using Godot;

namespace Project_Star.Core.Bases;

// 英雄基类，持有一份独立的英雄属性集，初始属性由子类覆写实现差异。
[GlobalClass]
public partial class HeroBase : Node
{
	// 英雄属性集
	[Export]
	public HeroAttributeSet AttributeSet { get; set; } = new();

	public override void _Ready()
	{
		base._Ready();
		ApplyInitialAttributes();
	}

	// 应用英雄初始属性，供子类覆写实现初始属性差异
	protected virtual void ApplyInitialAttributes()
	{
	}
}