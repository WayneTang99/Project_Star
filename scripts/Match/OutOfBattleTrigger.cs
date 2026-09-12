using System;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Match.Events;
using Project_Star.Systems;

namespace Project_Star.Match;

// 局外触发器（普通类）：非战斗事件到达时，将事件路由给场景中所有实现
// IMatchPassiveAbility 的实体。
public class OutOfBattleTrigger
{
	private readonly HeroManager _heroManager;
	private readonly CardManager _cardManager;

	public OutOfBattleTrigger(HeroManager heroManager, CardManager cardManager)
	{
		_heroManager = heroManager;
		_cardManager = cardManager;
		MatchEventBus.EventRaised += OnEventRaised;
	}

	// 注销订阅
	public void Dispose()
	{
		MatchEventBus.EventRaised -= OnEventRaised;
	}

	// 事件到达时触发被动
	private void OnEventRaised(MatchEventBase evt)
	{
		Type evtType = evt.GetType();
		TryTriggerHero(evt, evtType);
		TryTriggerCards(evt, evtType);
	}

	// 触发英雄身上的匹配被动
	private void TryTriggerHero(MatchEventBase evt, Type evtType)
	{
		HeroBase? hero = _heroManager.CurrentHero;
		if (hero is null)
		{
			return;
		}

		foreach (AriaAbilityBase ability in hero.Abilities)
		{
			if (ability is IMatchPassiveAbility passive && passive.ReactEventType == evtType)
			{
				var ctx = new BattleContext { Source = hero, Self = hero };
				if (ability.CanActivate(ctx))
				{
					ability.Activate(ctx);
				}
			}
		}
	}

	// 触发玩家拥有卡牌身上的匹配被动
	private void TryTriggerCards(MatchEventBase evt, Type evtType)
	{
		Godot.Collections.Array<CardBase> cards = _cardManager.PlayerCards;
		for (int i = 0; i < cards.Count; i++)
		{
			CardBase card = cards[i];
			if (card is null || !GodotObject.IsInstanceValid(card))
			{
				continue;
			}

			foreach (AriaAbilityBase ability in card.Abilities)
			{
				if (ability is IMatchPassiveAbility passive && passive.ReactEventType == evtType)
				{
					var ctx = new BattleContext { Source = card, Self = card };
					if (ability.CanActivate(ctx))
					{
						ability.Activate(ctx);
					}
				}
			}
		}
	}
}
