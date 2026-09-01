using Godot;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Core.Bases;

// 商人基类，持有一份独立的商店属性集，初始属性由子类覆写实现差异。
[GlobalClass]
public partial class MerchantBase : Node
{
	// 商店属性集
	[Export]
	public MerchantAttributeSet AttributeSet { get; set; } = new();

	public override void _Ready()
	{
		base._Ready();
		ApplyInitialAttributes();
	}

	// 应用商人初始属性，供子类覆写实现初始属性差异
	protected virtual void ApplyInitialAttributes()
	{
	}
}
