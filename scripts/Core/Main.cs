using Godot;
using Project_Star.Core.Managers;

namespace Project_Star.Core;

public partial class Main : Node
{
	public GameManager GameManager { get; private set; } = null!;

	public RoundTurnManager RoundTurnManager { get; private set; } = null!;

	public override void _Ready()
	{
		base._Ready();
		GameManager = new GameManager();
		AddChild(GameManager);

		RoundTurnManager = new RoundTurnManager();
		AddChild(RoundTurnManager);
	}
}