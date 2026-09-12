using Godot;
using Project_Star.Board.Manager;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;
using Project_Star.Match;
using Project_Star.Match.Events;
using Project_Star.Systems;

namespace Project_Star.UI;

// 极简可玩 UI：控制台式文字交互，验证游戏闭环
// 按 0 = 选英雄、1-3 = 选事件、B = 开始战斗、S = 出售卡牌、R = 刷新商店、Q = 放弃对局
// 事件组自动填入 3 个事件，供玩家选择
public partial class MinimalGameUI : Control
{
	private MatchManager _matchManager = null!;
	private GameManager _gameManager = null!;
	private HeroManager _heroManager = null!;
	private BoardManager _boardManager = null!;
	private CardManager _cardManager = null!;

	private bool _heroChosen;

	public override void _Ready()
	{
		base._Ready();

		_gameManager = GetNode<GameManager>("../GameManager");
		_heroManager = GetNode<HeroManager>("../HeroManager");
		_boardManager = GetNode<BoardManager>("../BoardManager");
		_cardManager = GetNode<CardManager>("../CardManager");
		_matchManager = GetNode<MatchManager>("../MatchManager");

		_gameManager.StateChangedEvent += OnStateChanged;
		_matchManager.EventsGeneratedEvent += OnEventsGenerated;
		MatchEventBus.EventRaised += OnMatchEventRaised;

		PrintMenu();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.Key0:
				OnSelectHero();
				break;
			case Key.Key1:
				OnSelectEvent(0);
				break;
			case Key.Key2:
				OnSelectEvent(1);
				break;
			case Key.Key3:
				OnSelectEvent(2);
				break;
			case Key.B:
				OnStartBattle();
				break;
			case Key.S:
				OnSellCard();
				break;
			case Key.R:
				OnRefreshShop();
				break;
			case Key.Q:
				OnSurrender();
				break;
		}
	}

	private void OnSelectHero()
	{
		if (_heroManager.AvailableHeroes.Count == 0)
		{
			GD.Print("没有可用英雄");
			return;
		}

		_gameManager.StartNewMatch();
		_heroManager.SelectHero(_heroManager.AvailableHeroes[0]);
		_heroChosen = true;
	}

	private void OnSelectEvent(int index)
	{
		if (_matchManager.CurrentEvents.Count <= index)
		{
			GD.Print("事件索引越界");
			return;
		}

		EventBase evt = _matchManager.CurrentEvents[index];
		if (evt is MonsterEvent or PvPEvent)
		{
			GD.Print("战斗事件请按 B 开始");
			return;
		}

		_matchManager.TryResolveEvent(evt);
	}

	private void OnStartBattle()
	{
		if (_matchManager.CurrentEvents.Count == 0)
		{
			GD.Print("无可用事件");
			return;
		}

		EventBase evt = _matchManager.CurrentEvents[0];
		if (evt is MonsterEvent or PvPEvent)
		{
			_matchManager.StartBattleForEvent(evt);
			return;
		}

		GD.Print("第一个事件非战斗事件");
	}

	private void OnSellCard()
	{
		if (_boardManager.Battlefield.GetCards().Count == 0
			&& _boardManager.Bench.GetCards().Count == 0)
		{
			GD.Print("没有可出售卡牌");
			return;
		}

		CardBase? card = _boardManager.Battlefield.GetCards().Count > 0
			? _boardManager.Battlefield.GetCards()[0]
			: _boardManager.Bench.GetCards()[0];
		_cardManager.SellCard(card, card.AttributeSet.Value);
	}

	private void OnRefreshShop()
	{
		// TODO: 商店刷新逻辑
		GD.Print("刷新商店（待实现）");
	}

	private void OnSurrender()
	{
		_gameManager.Surrender();
	}

	private void OnStateChanged(GameState newState)
	{
		GD.Print($"状态切换：{newState}");
		if (newState == GameState.Result)
		{
			_heroChosen = false;
			PrintResult();
		}
	}

	private void OnEventsGenerated(int round, int turn, Godot.Collections.Array<EventBase> events)
	{
		GD.Print($"== 第 {round} 轮第 {turn} 回合 ==");
		for (int i = 0; i < events.Count; i++)
		{
			GD.Print($"  [{i + 1}] {events[i].AttributeSet.EventDisplayName}");
		}
	}

	private void OnMatchEventRaised(MatchEventBase evt)
	{
		if (evt is BattleWonEvent)
		{
			GD.Print("战斗胜利！");
		}
		else if (evt is ItemPurchasedEvent)
		{
			GD.Print("购买成功！");
		}
		else if (evt is ItemSoldEvent)
		{
			GD.Print("出售成功！");
		}
	}

	private void PrintMenu()
	{
		GD.Print("== 极简可玩 UI ==");
		GD.Print("[0] 选英雄 [1-3] 选事件 [B] 开始战斗 [S] 出售卡牌 [R] 刷新商店 [Q] 放弃对局");
	}

	private void PrintResult()
	{
		GD.Print("== 对局结束 ==");
		GD.Print($"声望：{_heroManager.CurrentHero?.AttributeSet.Reputation.CurrentValue ?? 0}");
	}
}
