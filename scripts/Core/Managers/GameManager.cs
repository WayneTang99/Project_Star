using System;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Managers;

[GlobalClass]
public partial class GameManager : Node
{
	public GameState CurrentState { get; private set; } = GameState.MainMenu;

	public MatchEndReason? LastMatchEndReason { get; private set; }

	public event Action<GameState>? StateChangedEvent;

	public event Action<MatchEndReason>? MatchEndedEvent;

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

	public void Surrender()
	{
		EndMatch(MatchEndReason.Surrendered);
	}

	public void EndMatch(MatchEndReason reason)
	{
		LastMatchEndReason = reason;
		ChangeState(GameState.Result);
		MatchEndedEvent?.Invoke(reason);
	}

	public void ReturnToMainMenu()
	{
		LastMatchEndReason = null;
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