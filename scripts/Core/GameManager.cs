using System;
using Godot;

namespace Project_Star.Core;

[GlobalClass]
public partial class GameManager : Node
{
	public GameState CurrentState { get; private set; } = GameState.MainMenu;

	public event Action<GameState>? StateChangedEvent;

	public override void _Ready()
	{
		base._Ready();
		ChangeState(GameState.MainMenu);
	}

	public void StartNewMatch()
	{
		ChangeState(GameState.HeroSelect);
	}

	public void HeroSelected()
	{
		ChangeState(GameState.InMatch);
	}

	public void EndMatch()
	{
		ChangeState(GameState.Result);
	}

	public void ReturnToMainMenu()
	{
		ChangeState(GameState.MainMenu);
	}

	private void ChangeState(GameState newState)
	{
		if (CurrentState == newState)
		{
			return;
		}

		CurrentState = newState;
		StateChangedEvent?.Invoke(newState);
	}
}