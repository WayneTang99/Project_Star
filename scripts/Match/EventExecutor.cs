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

// 事件执行器（普通类）：负责战斗/非战斗事件的具体执行逻辑。
// 包含生成幽灵对手、处理战果结算、战斗胜利失败奖励惩罚等。
public class EventExecutor
{
	private readonly HeroManager _heroManager;
	private readonly CardManager _cardManager;
	private readonly BoardManager _boardManager;
	private readonly GameManager _gameManager;
	private readonly CombatManager _combatManager;
	private readonly RoundTurn _roundTurn;

	// PvP/怪物战斗固定奖励
	private const float PVP_WIN_WEALTH = 5f;
	private const float PVP_WIN_EXPERIENCE = 3f;
	private const float MONSTER_WIN_WEALTH = 2f;
	private const float MONSTER_WIN_EXPERIENCE = 1f;

	// 幽灵对手卡牌上限
	private const int GHOST_CARD_CAP = 6;

	// 当前战斗临时实体（用于战斗结束清理）
	private readonly List<Node> _tempCombatants = new();

	// 当前战斗是否为 PvP
	private bool _currentBattleIsPvP;

	// 友方英雄引用（用于战果判定）
	private HeroBase? _friendlyHero;

	public EventExecutor(
		HeroManager heroManager,
		CardManager cardManager,
		BoardManager boardManager,
		GameManager gameManager,
		CombatManager combatManager,
		RoundTurn roundTurn)
	{
		_heroManager = heroManager;
		_cardManager = cardManager;
		_boardManager = boardManager;
		_gameManager = gameManager;
		_combatManager = combatManager;
		_roundTurn = roundTurn;
	}

	// 启动战斗：收集双方卡牌并开始战斗
	public void StartBattle(EventBase evt, Node owner)
	{
		var friendlyCards = new List<ICombatant>();
		foreach (CardBase card in _boardManager.Battlefield.GetCards())
		{
			friendlyCards.Add(card);
		}

		HeroBase friendlyHero = _heroManager.CurrentHero!;
		_friendlyHero = friendlyHero;

		HeroBase enemyHero;
		List<ICombatant> enemyCards;

		if (evt is MonsterEvent monsterEvt)
		{
			_currentBattleIsPvP = false;
			enemyHero = monsterEvt.Monster!;
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
			(HeroBase hero, List<ICombatant> cards) = GenerateGhostOpponent(_roundTurn.CurrentRound, owner);
			enemyHero = hero;
			enemyCards = cards;
		}
		else
		{
			return;
		}

		_combatManager.StartBattle(friendlyHero, friendlyCards, enemyHero, enemyCards, _currentBattleIsPvP);
	}

	// 结算非战斗事件（商店/通用）并推进回合
	public bool ResolveEvent(EventBase evt, EventManager eventManager)
	{
		if (evt.State != EventState.Pending)
		{
			return false;
		}

		eventManager.ResolveEvent(evt);
		_roundTurn.CompleteTurn();
		return true;
	}

	// 处理战斗胜利事件：判胜负 → 发放奖励/惩罚 → 释放幽灵 → 回满血 → 推进回合
	public void HandleBattleOutcome(BattleWonEvent battleWon)
	{
		bool playerWon = ReferenceEquals(battleWon.Winner, _friendlyHero)
			|| ReferenceEquals(battleWon.Winner, _heroManager.CurrentHero);

		CleanupTempCombatants();
		RestoreHeroHealthAfterBattle();

		if (playerWon)
		{
			ApplyWinRewards(battleWon);
		}
		else
		{
			ApplyLossPenalty(battleWon);
		}

		_roundTurn.CompleteTurn();
	}

	// 自动对局：事件生成后自动选择战斗/非战斗事件推进回合
	public void AutoResolve(Godot.Collections.Array<EventBase> events)
	{
		foreach (EventBase evt in events)
		{
			if (evt is MonsterEvent or PvPEvent)
			{
				StartBattle(evt, null!);
				return;
			}
		}

		// 无战斗事件，结算第一个非战斗事件并推进
		foreach (EventBase evt in events)
		{
			if (evt.State == EventState.Pending)
			{
				evt.Resolve();
				_roundTurn.CompleteTurn();
				return;
			}
		}
	}

	// 玩家胜利：怪物战按剩余血量比例发放财富/经验 + 随机卡牌；PvP 固定奖励 + 记录连续胜利
	private void ApplyWinRewards(BattleWonEvent battleWon)
	{
		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		if (battleWon.IsPvP)
		{
			hero.AttributeSet.Wealth.SetCurrentValue(hero.AttributeSet.Wealth.CurrentValue + PVP_WIN_WEALTH);
			hero.AttributeSet.Experience.SetCurrentValue(hero.AttributeSet.Experience.CurrentValue + PVP_WIN_EXPERIENCE);
			_gameManager.RecordPvPWin();
		}
		else
		{
			// 怪物战奖励按敌方剩余血量比例缩放（打得越漂亮奖励越少，鼓励速杀）
			float ratio = 1f - battleWon.EnemyRemainingRatio;
			float wealthGain = Mathf.Max(1f, MONSTER_WIN_WEALTH * ratio);
			float expGain = Mathf.Max(1f, MONSTER_WIN_EXPERIENCE * ratio);
			hero.AttributeSet.Wealth.SetCurrentValue(hero.AttributeSet.Wealth.CurrentValue + wealthGain);
			hero.AttributeSet.Experience.SetCurrentValue(hero.AttributeSet.Experience.CurrentValue + expGain);

			// 怪物战胜利奖励随机卡牌
			if (_cardManager.CardTemplates.Count > 0)
			{
				CardBase template = _cardManager.CardTemplates[GD.RandRange(0, _cardManager.CardTemplates.Count - 1)];
				CardBase reward = _cardManager.CreateCard(template);
				_cardManager.AddCardToPlayer(reward);
			}
		}
	}

	// 玩家失败：PvP 失败按当前轮扣声望，怪物失败无惩罚；声望 ≤0 立即结束对局
	private void ApplyLossPenalty(BattleWonEvent battleWon)
	{
		if (!battleWon.IsPvP)
		{
			return;
		}

		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		_gameManager.RecordPvPLoss(hero, _roundTurn.CurrentRound);

		// PvP 失败后立即检查声望：≤0 时以失败结束对局
		if (hero.AttributeSet.Reputation.CurrentValue <= 0f)
		{
			_gameManager.EndMatch(MatchEndReason.Defeat);
		}
	}

	// 生成幽灵对手：模板池随机英雄深拷贝 + 按轮次缩放卡牌
	private (HeroBase Hero, List<ICombatant> Cards) GenerateGhostOpponent(int round, Node owner)
	{
		HeroBase template = _heroManager.AvailableHeroes[GD.RandRange(0, _heroManager.AvailableHeroes.Count - 1)];
		HeroBase ghostHero = (HeroBase)template.Duplicate();
		ghostHero.AttributeSet = (HeroAttributeSet)AttributeSetCopier.DeepCopy(template.AttributeSet);
		owner.AddChild(ghostHero);
		_tempCombatants.Add(ghostHero);

		int cardCount = Mathf.Clamp(1 + round / 2, 1, GHOST_CARD_CAP);
		int level = Mathf.Clamp(1 + round / 4, 1, 5);

		var cards = new List<ICombatant>();
		for (int i = 0; i < cardCount && _cardManager.CardTemplates.Count > 0; i++)
		{
			CardBase cardTemplate = _cardManager.CardTemplates[GD.RandRange(0, _cardManager.CardTemplates.Count - 1)];
			CardBase ghostCard = _cardManager.CreateCard(cardTemplate);
			ghostCard.AttributeSet.Level.SetCurrentValue(level);
			_tempCombatants.Add(ghostCard);
			cards.Add(ghostCard);
		}

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
	}
}
