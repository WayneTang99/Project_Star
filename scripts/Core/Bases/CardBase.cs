using Godot;

namespace Project_Star.Core.Bases;

[GlobalClass]
public abstract partial class CardBase : Node
{
	public CardAttributeSet AttributeSet { get; set; } = null!;
}