using Godot;

namespace Project_Star.Core;

[GlobalClass]
public partial class HeroBase : Node
{
	[Export]
	public HeroAttributeSet AttributeSet { get; set; } = new();

	public override void _Ready()
	{
		base._Ready();
	}
}