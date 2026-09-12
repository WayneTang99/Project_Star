using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Board.Manager;
using Project_Star.Combat.Managers;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;
using Project_Star.Match.Events;
using Project_Star.Systems;

namespace Project_Star.Match;

// 对局流程协调器：串联 事件生成 → 战斗触发 → 战果结算（奖励/惩罚）→ 回合推进，
// 是对局闭环的唯一编排入口（唯一调用 RoundTurnManager.CompleteTurn 与 CombatManager.StartBattle 的模块）。
public partial class MatchFlowCoordinator : Node
{
	// 怪物战胜利奖励：财富
	private const float MONSTER_WIN_WEALTH = 10f;
	// 怪物战胜利奖励：经验
	private const float MONSTER_WIN_EXPERIENCE = 5f;
	// PvP 战胜利奖励：财富
	private const float PVP_WIN_WEALTH = 15f;
	// PvP 战胜利奖励：经验
	private const float PVP_WIN_EXPERIENCE = 8f;
	// 幽灵对手卡牌数量上限（受棋盘容量约束）
	private const int GHOST_CARD_CAP = 6;

	private GameManager _gameManager = null!;
	private HeroManager _heroManager = null!;
	private CardManager _cardManager = null!;
	private BoardManager _boardManager = null!;
	private RoundTurnManager _roundTurnManager = null!;
	private EventManager _eventManager = null!;
	private CombatManager _combatManager = null!;

	// 当前战斗是否属于 PvP（决定奖励与惩罚分支）
	private bool _currentBattleIsPvP;

	// 我方英雄引用（战斗开始时缓存，用于战果判定；CombatManager 在结算后已清空己方引用）
	private ICombatant? _friendlyHero;

	// 幽灵对手临时实体（战斗结束时统一释放）
	private readonly List<Node> _tempCombatants = new();

	// 自动对局标志：为 true 时自动选择并推进回合，无需 UI
	public bool AutoPlay { get; set; }

	// 对局流程日志（供测试 / 观察闭环推进）
	public event Action<string>? FlowLog;

	public override void _Ready()
	{
		base._Ready();
		_gameManager = GetNode<GameManager>("../GameManager");
		_heroManager = GetNode<HeroManager>("../HeroManager");
		_cardManager = GetNode<CardManager>("../CardManager");
		_boardManager = GetNode<BoardManager>("../BoardManager");
		_roundTurnManager = GetNode<RoundTurnManager>("../RoundTurnManager");
		_eventManager = GetNode<EventManager>("../EventManager");
		_combatManager = GetNode<CombatManager>("../CombatManager");

		MatchEventBus.EventRaised += OnMatchEventRaised;
		_gameManager.StateChangedEvent += OnStateChanged;
		_eventManager.EventsGeneratedEvent += OnEventsGenerated;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		MatchEventBus.EventRaised -= OnMatchEventRaised;
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}

		if (_eventManager is not null)
		{
			_eventManager.EventsGeneratedEvent -= OnEventsGenerated;
		}
	}

	// UI/场景入口：点击战斗事件（怪物/PvP）时发起战斗
	public void StartBattleForEvent(EventBase evt)
	{
		// 收集我方战场卡牌（实现 ICombatant）
		var friendlyCards = new List<ICombatant>();
		foreach (CardBase card in _boardManager.Battlefield.GetCards())
		{
			friendlyCards.Add(card);
		}

		HeroBase? friendlyHero = _heroManager.CurrentHero;
		_friendlyHero = friendlyHero;

		HeroBase? enemyHero;
		List<ICombatant> enemyCards;

		if (evt is MonsterEvent monsterEvt)
		{
			_currentBattleIsPvP = false;
			enemyHero = monsterEvt.Monster;
			enemyCards = new List<ICombatant>();
			if (monsterEvt.Monster is MonsterBase monster)
			{
				foreach (CardBase card in monster.Deck)
				{
					enemyCards.Add(card);
				}
			}
		}
		else if (evt is PvPEvent)
		{
			_currentBattleIsPvP = true;
			(HeroBase hero, List<ICombatant> cards) = GenerateGhostOpponent(_roundTurnManager.CurrentRound);
			enemyHero = hero;
			enemyCards = cards;
		}
		else
		{
			FlowLog?.Invoke("StartBattleForEvent：非战斗事件，忽略");
			return;
		}

		FlowLog?.Invoke($"开始战斗：PvP={_currentBattleIsPvP} 敌方卡牌={enemyCards.Count}");
		_combatManager.StartBattle(friendlyHero, friendlyCards, enemyHero, enemyCards);
	}

	// UI/场景入口：结算非战斗事件（商店/通用）并推进回合；非 Pending 状态返回 false
	public bool TryResolveEvent(EventBase evt)
	{
		if (evt.State != EventState.Pending)
		{
			return false;
		}

		_eventManager.ResolveEvent(evt);
		_roundTurnManager.CompleteTurn();
		FlowLog?.Invoke($"结算事件并推进回合：{evt.AttributeSet.EventDisplayName}");
		return true;
	}

	// 局内事件总线回调：战斗胜利事件到来时结算战果并推进回合
	private void OnMatchEventRaised(MatchEventBase evt)
	{
		if (evt is BattleWonEvent battleWon)
		{
			HandleBattleOutcome(battleWon);
		}
	}

	// 战果结算：判胜负 → 发放奖励/惩罚 → 释放幽灵实体 → 推进回合
	private void HandleBattleOutcome(BattleWonEvent battleWon)
	{
		bool playerWon = ReferenceEquals(battleWon.Winner, _friendlyHero)
			|| ReferenceEquals(battleWon.Winner, _heroManager.CurrentHero);

		// 释放幽灵对手临时实体（战斗已结束，避免泄漏）
		CleanupTempCombatants();

		// 战斗结束后英雄生命回满
		RestoreHeroHealthAfterBattle();

		if (playerWon)
		{
			ApplyWinRewards();
		}
		else
		{
			ApplyLossPenalty();
		}

		_roundTurnManager.CompleteTurn();
	}

	// 玩家胜利：按战斗类型发放财富/经验奖励，PvP 额外记录连续胜利
	private void ApplyWinRewards()
	{
		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		float wealthGain = _currentBattleIsPvP ? PVP_WIN_WEALTH : MONSTER_WIN_WEALTH;
		float expGain = _currentBattleIsPvP ? PVP_WIN_EXPERIENCE : MONSTER_WIN_EXPERIENCE;
		hero.AttributeSet.Wealth.SetCurrentValue(hero.AttributeSet.Wealth.CurrentValue + wealthGain);
		hero.AttributeSet.Experience.SetCurrentValue(hero.AttributeSet.Experience.CurrentValue + expGain);
		FlowLog?.Invoke($"战斗胜利：财富+{wealthGain} 经验+{expGain}");

		if (_currentBattleIsPvP)
		{
			_gameManager.RecordPvPWin();
			FlowLog?.Invoke("PvP 胜利，记录连续胜利");
		}
	}

	// 玩家失败：PvP 失败按当前轮扣声望，怪物失败无惩罚（战斗直接结束）
	private void ApplyLossPenalty()
	{
		if (!_currentBattleIsPvP)
		{
			FlowLog?.Invoke("怪物战斗失败：无惩罚");
			return;
		}

		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		_gameManager.RecordPvPLoss(hero, _roundTurnManager.CurrentRound);
		FlowLog?.Invoke($"PvP 失败：按第 {_roundTurnManager.CurrentRound} 轮扣减声望");
	}

	// 生成幽灵对手：模板池随机英雄深拷贝 + 按轮次缩放卡牌（数量与等级）
	private (HeroBase Hero, List<ICombatant> Cards) GenerateGhostOpponent(int round)
	{
		// 随机选取一个英雄模板做深拷贝（隔离玩家实例，避免属性写回污染）
		HeroBase template = _heroManager.AvailableHeroes[GD.RandRange(0, _heroManager.AvailableHeroes.Count - 1)];
		HeroBase ghostHero = (HeroBase)template.Duplicate();
		ghostHero.AttributeSet = (HeroAttributeSet)AttributeSetCopier.DeepCopy(template.AttributeSet);
		AddChild(ghostHero);
		_tempCombatants.Add(ghostHero);

		// 卡牌数量随轮次增长（1 + round/2，上限 6）；卡牌等级随轮次增长（1 + round/4，上限 5）
		int cardCount = Mathf.Clamp(1 + round / 2, 1, GHOST_CARD_CAP);
		int level = Mathf.Clamp(1 + round / 4, 1, 5);

		var cards = new List<ICombatant>();
		for (int i = 0; i < cardCount && _cardManager.CardTemplates.Count > 0; i++)
		{
			CardBase cardTemplate = _cardManager.CardTemplates[GD.RandRange(0, _cardManager.CardTemplates.Count - 1)];
			CardBase ghostCard = _cardManager.CreateCard(cardTemplate);
			ghostCard.AttributeSet.Level.SetCurrentValue(level);
			// CreateCard 已将其挂到 CardManager 下，此处再 AddChild 会报"已存在父节点"；由 _tempCombatants 统一释放
			_tempCombatants.Add(ghostCard);
			cards.Add(ghostCard);
		}

		FlowLog?.Invoke($"生成幽灵对手：{ghostHero.AttributeSet.HeroDisplayName} 卡牌={cardCount} 等级={level}");
		return (ghostHero, cards);
	}

	// 释放幽灵对手临时实体
	private void CleanupTempCombatants()
	{
		foreach (Node node in _tempCombatants)
		{
			node.QueueFree();
		}

		_tempCombatants.Clear();
	}

	// 战斗结束后英雄生命回满
	private void RestoreHeroHealthAfterBattle()
	{
		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		hero.AttributeSet.Health.SetCurrentValue(hero.AttributeSet.MaxHealth.CurrentValue);
		FlowLog?.Invoke("战斗结束：英雄生命已回满");
	}

	// 自动对局：事件生成后自动选择战斗/非战斗事件推进回合
	private void OnEventsGenerated(int round, int turn, Godot.Collections.Array<EventBase> events)
	{
		if (!AutoPlay)
		{
			return;
		}

		FlowLog?.Invoke($"自动对局：第 {round} 轮第 {turn} 回合，事件 {events.Count} 个");

		// 优先进入战斗事件（怪物/PvP）
		foreach (EventBase evt in events)
		{
			if (evt is MonsterEvent or PvPEvent)
			{
				FlowLog?.Invoke($"自动对局：进入战斗 {evt.AttributeSet.EventDisplayName}");
				StartBattleForEvent(evt);
				return;
			}
		}

		// 无战斗事件，结算第一个非战斗事件并推进
		foreach (EventBase evt in events)
		{
			if (TryResolveEvent(evt))
			{
				return;
			}
		}
	}

	// 状态切换：对局结束（Result）时停止自动对局
	private void OnStateChanged(GameState newState)
	{
		if (newState == GameState.Result)
		{
			AutoPlay = false;
			FlowLog?.Invoke("对局结束，停止自动对局");
		}
	}
}
