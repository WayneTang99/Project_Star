using System;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Systems;

// 主状态机管理器：广播状态切换，EndMatch / Surrender 强制结束对局。
[GlobalClass]
public partial class GameManager : Node
{
	// 当前游戏状态
	public GameState CurrentState { get; private set; } = GameState.MainMenu;

	// 最近一次对局结束原因；未结束时为 null
	public MatchEndReason? LastMatchEndReason { get; private set; }

	// 状态切换事件，参数为切换后的状态
	public event Action<GameState>? StateChangedEvent;

	// 对局结束事件，参数为结束原因
	public event Action<MatchEndReason>? MatchEndedEvent;

	public override void _Ready()
	{
		base._Ready();
		ChangeState(GameState.MainMenu);
	}

	// 开始新对局：进入英雄选择阶段
	public void StartNewMatch()
	{
		ChangeState(GameState.HeroSelect);
	}

	// 英雄选择完成：进入局内阶段
	public void HeroSelected()
	{
		ChangeState(GameState.InMatch);
	}

	// 玩家主动认输，强制结束对局
	public void Surrender()
	{
		EndMatch(MatchEndReason.Surrendered);
	}

	// 以指定原因结束对局，广播状态切换与结束事件
	public void EndMatch(MatchEndReason reason)
	{
		LastMatchEndReason = reason;
		ChangeState(GameState.Result);
		MatchEndedEvent?.Invoke(reason);
	}

	// 返回主菜单，清除上一次对局结束原因
	public void ReturnToMainMenu()
	{
		LastMatchEndReason = null;
		ChangeState(GameState.MainMenu);
	}

	// 切换状态并广播事件；状态相同时忽略
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