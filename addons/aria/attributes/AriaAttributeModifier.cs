using Godot;

namespace Aria;

// 属性修饰器：描述对单个属性的一次运算（加/减/乘/覆盖）。
[GlobalClass]
public partial class AriaAttributeModifier : Resource
{
	// 目标属性 key
	[Export]
	public string AttributeKey { get; set; } = "";

	// 运算方式
	[Export]
	public AriaAttributeOperation Operation { get; set; }

	// 数值
	[Export]
	public float Magnitude { get; set; }
}