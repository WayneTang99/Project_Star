using System;
using Project_Star.Core.Types;
using Project_Star.Systems;

namespace Project_Star.Match;

// 轮次状态机（普通类）：管理 8 回合/轮、轮次推进、声望失败判定。
public class RoundTurn
{
	private readonly GameManager _gameManager;
	private readonly HeroManager _heroManager;

	// 每轮回合数
	public const int TURNS_PER_ROUND = 8;

	// 当前轮数；未开始时为 0
	public int CurrentRound { get; private set; }

	// 当前回合数；未开始时为 0
	public int CurrentTurn { get; private set; }

	// 回合开始事件，参数为（轮数, 回合数）
	public event Action<int, int>? TurnStartedEvent;

	// 轮开始事件，参数为轮数
	public event Action<int>? RoundStartedEvent;

	// 回合完成事件
	public event Action? TurnCompletedEvent;

	public RoundTurn(GameManager gameManager, HeroManager heroManager)
	{
		_gameManager = gameManager;
		_heroManager = heroManager;
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

	// 轮末判定：英雄声望 ≤ 0 时以失败结束对局
	private void CheckDefeat()
	{
		if (_heroManager.CurrentHero?.AttributeSet.Reputation.CurrentValue <= 0f)
		{
			_gameManager.EndMatch(MatchEndReason.Defeat);
		}
	}
}
