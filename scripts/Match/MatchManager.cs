using System;
using Godot;
using Project_Star.Board.Manager;
using Project_Star.Combat.Managers;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;
using Project_Star.Match.Events;
using Project_Star.Systems;

namespace Project_Star.Match;

// 对局管理器（Node）：整合轮次、事件管理、事件执行、局外被动路由。
// 替代原 RoundTurnManager + EventManager（Node） + MatchFlowCoordinator + MatchEventDispatcher。
public partial class MatchManager : MatchManagerBase
{
	[Export] public bool AutoPlay;

	// 普通类组件
	private RoundTurn _roundTurn = null!;
	private EventManager _eventManager = null!;
	private EventExecutor _eventExecutor = null!;
	private OutOfBattleTrigger _trigger = null!;

	// 引用
	private CombatManager _combatManager = null!;
	private HeroManager _heroManager = null!;

	// 当前回合事件组（供 UI 读取）
	public Godot.Collections.Array<EventBase> CurrentEvents => _eventManager.CurrentEvents;

	// 自动对局事件回调（供测试订阅）
	public event Action<int, int, Godot.Collections.Array<EventBase>>? EventsGeneratedEvent;

	protected override void OnInitialize()
	{
		base.OnInitialize();

		var gm = GetNode<GameManager>("../GameManager");
		var hm = GetNode<HeroManager>("../HeroManager");
		var cm = GetNode<CardManager>("../CardManager");
		var bm = GetNode<BoardManager>("../BoardManager");
		var cbm = GetNode<CombatManager>("../CombatManager");

		_heroManager = hm;
		_combatManager = cbm;

		_roundTurn = new RoundTurn(gm, hm);
		_eventManager = new EventManager(this);
		_eventManager.Initialize();
		_eventExecutor = new EventExecutor(hm, cm, bm, gm, cbm, _roundTurn);
		_trigger = new OutOfBattleTrigger(hm, cm);

		gm.StateChangedEvent += OnStateChanged;
		_eventManager.EventsGeneratedEvent += OnEventsGenerated;
		_roundTurn.TurnStartedEvent += OnTurnStarted;
	}

	protected override void OnShutdown()
	{
		var gm = GetNode<GameManager>("../GameManager");
		gm.StateChangedEvent -= OnStateChanged;

		if (_eventManager is not null)
		{
			_eventManager.EventsGeneratedEvent -= OnEventsGenerated;
		}

		if (_roundTurn is not null)
		{
			_roundTurn.TurnStartedEvent -= OnTurnStarted;
		}

		if (_trigger is not null)
		{
			_trigger.Dispose();
		}

		base.OnShutdown();
	}

	// 对局开始：重置轮次并生成第一回合事件
	public void StartMatch()
	{
		_roundTurn.StartMatch();
		_eventManager.GenerateEvents(_roundTurn.CurrentRound, _roundTurn.CurrentTurn);
	}

	// 结束对局
	public void EndMatch()
	{
		_eventManager.ClearEvents();
	}

	// UI 入口：点击战斗事件（怪物/PvP）时发起战斗
	public void StartBattleForEvent(EventBase evt)
	{
		_eventExecutor.StartBattle(evt, this);
	}

	// UI 入口：结算非战斗事件（商店/通用）并推进回合
	public bool TryResolveEvent(EventBase evt)
	{
		return _eventExecutor.ResolveEvent(evt, _eventManager);
	}

	// 结算战斗胜利事件
	public void HandleBattleOutcome(BattleWonEvent battleWon)
	{
		_eventExecutor.HandleBattleOutcome(battleWon);
	}

	// 获取当前轮数
	public int GetCurrentRound()
	{
		return _roundTurn.CurrentRound;
	}

	// 获取当前回合数
	public int GetCurrentTurn()
	{
		return _roundTurn.CurrentTurn;
	}

	private void OnTurnStarted(int round, int turn)
	{
		_eventManager.GenerateEvents(round, turn);
	}

	private void OnEventsGenerated(int round, int turn, Godot.Collections.Array<EventBase> events)
	{
		EventsGeneratedEvent?.Invoke(round, turn, events);

		if (AutoPlay)
		{
			_eventExecutor.AutoResolve(events);
		}
	}

	private void OnStateChanged(GameState newState)
	{
		if (newState == GameState.InMatch)
		{
			StartMatch();
		}
		else if (newState == GameState.Result)
		{
			AutoPlay = false;
			EndMatch();
		}
	}
}
