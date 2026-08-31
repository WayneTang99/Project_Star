using System;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Managers;

[GlobalClass]
public partial class RoundTurnManager : Node
{
	public const int TURNS_PER_ROUND = 8;

	private GameManager _gameManager = null!;

	public int CurrentRound { get; private set; }

	public int CurrentTurn { get; private set; }

	public bool IsPvPTurn => CurrentTurn == TURNS_PER_ROUND;

	public event Action<int, int>? TurnStartedEvent;

	public event Action<int>? RoundStartedEvent;

	public event Action? TurnCompletedEvent;

	public override void _Ready()
	{
		base._Ready();
		_gameManager = GetNode<GameManager>("../GameManager");
		_gameManager.StateChangedEvent += OnStateChanged;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}
	}

	public void StartMatch()
	{
		CurrentRound = 1;
		CurrentTurn = 1;
		RoundStartedEvent?.Invoke(CurrentRound);
		TurnStartedEvent?.Invoke(CurrentRound, CurrentTurn);
	}

	public void CompleteTurn()
	{
		TurnCompletedEvent?.Invoke();

		if (CurrentTurn < TURNS_PER_ROUND)
		{
			CurrentTurn++;
			TurnStartedEvent?.Invoke(CurrentRound, CurrentTurn);
			return;
		}

		CheckDefeat();

		CurrentRound++;
		CurrentTurn = 1;
		RoundStartedEvent?.Invoke(CurrentRound);
		TurnStartedEvent?.Invoke(CurrentRound, CurrentTurn);
	}

	private void OnStateChanged(GameState newState)
	{
		switch (newState)
		{
			case GameState.InMatch:
				StartMatch();
				break;
			case GameState.Result:
			case GameState.MainMenu:
			case GameState.HeroSelect:
				CurrentRound = 0;
				CurrentTurn = 0;
				break;
		}
	}

	private void CheckDefeat()
	{
	}
}