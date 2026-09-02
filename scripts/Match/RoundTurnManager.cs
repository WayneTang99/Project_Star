using System;
using Godot;
using Project_Star.Core.Types;
using Project_Star.Systems;

namespace Project_Star.Match;

// 局内轮次管理器：8 回合 / 轮、末回合固定 PvP、轮末声望失败判定。
[GlobalClass]
public partial class RoundTurnManager : Node
{
	// 每轮回合数
	public const int TURNS_PER_ROUND = 8;

	private GameManager _gameManager = null!;

	private HeroManager _heroManager = null!;

	// 当前轮数；未开始时为 0
	public int CurrentRound { get; private set; }

	// 当前回合数；未开始时为 0
	public int CurrentTurn { get; private set; }

	// 是否为 PvP 回合（本轮最后一回合）
	public bool IsPvPTurn => CurrentTurn == TURNS_PER_ROUND;

	// 回合开始事件，参数为（轮数, 回合数）
	public event Action<int, int>? TurnStartedEvent;

	// 轮开始事件，参数为轮数
	public event Action<int>? RoundStartedEvent;

	// 回合完成事件
	public event Action? TurnCompletedEvent;

	public override void _Ready()
	{
		base._Ready();
		_gameManager = GetNode<GameManager>("../GameManager");
		_gameManager.StateChangedEvent += OnStateChanged;
		_heroManager = GetNode<HeroManager>("../HeroManager");
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}
	}

	// 开始对局：重置轮次并从第 1 轮第 1 回合开始广播
	public void StartMatch()
	{
		CurrentRound = 1;
		CurrentTurn = 1;
		RoundStartedEvent?.Invoke(CurrentRound);
		TurnStartedEvent?.Invoke(CurrentRound, CurrentTurn);
	}

	// 完成当前回合；本轮回完则进入下一轮，并做声望失败判定
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

	// 进入局内即开始，离开局内则清零轮次
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

	// 轮末判定：英雄声望 ≤ 0 时以失败结束对局
	private void CheckDefeat()
	{
		if (_heroManager.CurrentHero?.AttributeSet.Reputation.CurrentValue <= 0f)
		{
			_gameManager.EndMatch(MatchEndReason.Defeat);
		}
	}
}