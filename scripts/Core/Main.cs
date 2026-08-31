using Godot;

namespace Project_Star.Core;

public partial class Main : Node
{
	public GameManager GameManager { get; private set; } = null!;

	public override void _Ready()
	{
		base._Ready();
		GameManager = GetNode<GameManager>("GameManager");
	}
}