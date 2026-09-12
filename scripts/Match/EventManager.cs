using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Pools;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;

namespace Project_Star.Match;

// 事件管理器（普通类）：通过 PoolBase 管理事件模板池、按回合规则排程、维护当前回合事件组与结算。
public class EventManager
{
	private readonly Node _owner;
	private PoolBase<EventBase> _pool = null!;

	// 未出现事件权重倍率
	private const float UNSEEN_MULTIPLIER = 2f;

	// 本局已出现事件（按 EventKey 记录）
	private readonly HashSet<StringName> _seenEvents = new();

	// 全部事件模板（反射收集，每种一张）
	public Godot.Collections.Array<EventBase> EventTemplates { get; } = new();

	// 当前回合的事件组
	public Godot.Collections.Array<EventBase> CurrentEvents { get; } = new();

	// 怪物事件子管理器
	public MonsterEventManager MonsterEventManager { get; } = new();

	// 可注入随机数生成器
	public RandomNumberGenerator Rng { get; set; } = new();

	// 事件组生成事件
	public event Action<int, int, Godot.Collections.Array<EventBase>>? EventsGeneratedEvent;

	// 事件结算事件
	public event Action<EventBase>? EventResolvedEvent;

	public EventManager(Node owner)
	{
		_owner = owner;
	}

	// 初始化：注册所有事件模板（通过 PoolBase 反射收集）
	public void Initialize()
	{
		_pool = new PoolBase<EventBase>(_owner);
		_pool.RegisterTemplates();

		foreach (EventBase template in _pool.Templates)
		{
			EventTemplates.Add(template);
		}

		MonsterEventManager.RegisterTemplates(_owner);
	}

	// 从模板复制一份事件实例并挂载为 owner 子节点
	public EventBase CreateEvent(EventBase template)
	{
		return _pool.CreateInstance(template);
	}

	// 将事件加入当前回合事件组
	public void AddEvent(EventBase evt)
	{
		CurrentEvents.Add(evt);
	}

	// 清空当前回合事件组并释放事件实例
	public void ClearEvents()
	{
		foreach (EventBase evt in CurrentEvents)
		{
			evt.QueueFree();
		}

		CurrentEvents.Clear();
	}

	// 结算事件并广播；非 Pending 状态忽略
	public void ResolveEvent(EventBase evt)
	{
		if (evt.State != EventState.Pending)
		{
			return;
		}

		evt.Resolve();
		EventResolvedEvent?.Invoke(evt);
	}

	// 按回合规则生成事件组并广播
	public void GenerateEvents(int round, int turn)
	{
		if (round == 1 && turn == 1)
		{
			_seenEvents.Clear();
		}

		List<EventBase> eligible = FilterEligible(round);

		if (turn == RoundTurn.TURNS_PER_ROUND)
		{
			CreatePvPEvents();
		}
		else if (turn == 4)
		{
			CreateMonsterEvents(eligible);
		}
		else
		{
			CreateDefaultEvents(eligible);
		}

		if (CurrentEvents.Count > 0)
		{
			EventsGeneratedEvent?.Invoke(round, turn, CurrentEvents);
		}
	}

	// 按轮次范围过滤可排布模板
	private List<EventBase> FilterEligible(int round)
	{
		var eligible = new List<EventBase>();
		foreach (EventBase template in EventTemplates)
		{
			EventAttributeSet attr = template.AttributeSet;
			if (attr.MinRound.CurrentValue <= round && round <= attr.MaxRound.CurrentValue)
			{
				eligible.Add(template);
			}
		}

		return eligible;
	}

	// 创建 PvP 事件：固定 3 个镜像 PvP 事件（各对应不同幽灵快照），三选一
	private void CreatePvPEvents()
	{
		foreach (EventBase template in EventTemplates)
		{
			if (template is PvPEvent)
			{
				for (int i = 0; i < 3; i++)
				{
					EventBase evt = CreateEvent(template);
					AddEvent(evt);
					MarkSeen(evt);
				}

				return;
			}
		}
	}

	// 创建怪物事件组：固定 3 个怪物事件
	private void CreateMonsterEvents(List<EventBase> eligible)
	{
		List<EventBase> monsterEvents = eligible.FindAll(evt => evt is MonsterEvent);
		if (monsterEvents.Count == 0 || MonsterEventManager.MonsterTemplates.Count == 0)
		{
			CreateDefaultEvents(eligible);
			return;
		}

		for (int i = 0; i < 3; i++)
		{
			MonsterEvent evt = (MonsterEvent)CreateEvent(monsterEvents[i % monsterEvents.Count]);
			MonsterBase monster = MonsterEventManager.CreateMonster(
				MonsterEventManager.MonsterTemplates[i % MonsterEventManager.MonsterTemplates.Count], evt);
			evt.Monster = monster;
			AddEvent(evt);
			MarkSeen(evt);
		}
	}

	// 创建默认 3 选事件组：先选 1 个商店事件，再补足到 3 个
	private void CreateDefaultEvents(List<EventBase> eligible)
	{
		List<EventBase> pool = eligible.FindAll(evt => evt is not MonsterEvent and not PvPEvent);
		if (pool.Count == 0)
		{
			return;
		}

		List<EventBase> remaining = new(pool);

		// 优先选 1 个商店事件
		List<EventBase> shopPool = remaining.FindAll(evt => evt is ShopEvent);
		if (shopPool.Count > 0)
		{
			EventBase shop = PickWeighted(shopPool);
			EventBase shopEvt = CreateEvent(shop);
			AddEvent(shopEvt);
			MarkSeen(shopEvt);
			remaining.Remove(shop);
		}

		// 其余按权重补足到 3 个
		int target = Math.Min(3, pool.Count);
		while (CurrentEvents.Count < target && remaining.Count > 0)
		{
			EventBase picked = PickWeighted(remaining);
			EventBase evt = CreateEvent(picked);
			AddEvent(evt);
			MarkSeen(evt);
			remaining.Remove(picked);
		}
	}

	// 按权重随机选取一个模板
	private EventBase PickWeighted(List<EventBase> pool)
	{
		float total = 0f;
		foreach (EventBase template in pool)
		{
			total += GetSelectionWeight(template);
		}

		float roll = Rng.RandfRange(0f, total);
		float accumulated = 0f;
		foreach (EventBase template in pool)
		{
			accumulated += GetSelectionWeight(template);
			if (roll <= accumulated)
			{
				return template;
			}
		}

		return pool[pool.Count - 1];
	}

	// 计算模板排布权重
	private float GetSelectionWeight(EventBase template)
	{
		float baseWeight = template.AttributeSet.Weight.CurrentValue;
		bool unseen = !_seenEvents.Contains(template.AttributeSet.EventKey);
		return baseWeight * (unseen ? UNSEEN_MULTIPLIER : 1f);
	}

	// 记录事件为本局已出现
	private void MarkSeen(EventBase evt)
	{
		_seenEvents.Add(evt.AttributeSet.EventKey);
	}
}
