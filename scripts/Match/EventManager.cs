using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;

namespace Project_Star.Match;

// 事件管理器：回合开始生成事件组（按回合规则排程），维护当前回合事件与结算。
[GlobalClass]
public partial class EventManager : Node
{
	// 未出现事件权重倍率：本局尚未出现的事件排布权重翻倍
	private const float UNSEEN_MULTIPLIER = 2f;

	private RoundTurnManager _roundTurnManager = null!;

	// 本局已出现事件（按 EventKey 记录，用于未出现加权；新对局首回合重置）
	private readonly HashSet<StringName> _seenEvents = new();

	// 全部事件模板（反射收集，每种一张）
	public Godot.Collections.Array<EventBase> EventTemplates { get; private set; } = new();

	// 当前回合的事件组
	public Godot.Collections.Array<EventBase> CurrentEvents { get; private set; } = new();

	// 怪物事件子管理器
	public MonsterEventManager MonsterEventManager { get; } = new();

	// 商店事件子管理器
	public ShopEventManager ShopEventManager { get; } = new();

	// 局内事件分发器：路由被动能力对局内事件的响应
	public MatchEventDispatcher MatchEventDispatcher { get; } = new();

	// 可注入随机数生成器：测试可设置 Seed 保证排程确定性
	public RandomNumberGenerator Rng { get; set; } = new();

	// 事件组生成事件，参数为（轮数, 回合数, 事件组）
	public event Action<int, int, Godot.Collections.Array<EventBase>>? EventsGeneratedEvent;

	// 事件结算事件
	public event Action<EventBase>? EventResolvedEvent;

	public override void _Ready()
	{
		base._Ready();
		_roundTurnManager = GetNode<RoundTurnManager>("../RoundTurnManager");
		_roundTurnManager.TurnStartedEvent += OnTurnStarted;
		RegisterEventTemplates();
		MonsterEventManager.RegisterTemplates(this);
		ShopEventManager.RegisterTemplates(this);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		MatchEventDispatcher.Dispose();
		if (_roundTurnManager is not null)
		{
			_roundTurnManager.TurnStartedEvent -= OnTurnStarted;
		}
	}

	// 从模板复制一份事件实例并挂载为本节点子节点
	public EventBase CreateEvent(EventBase template)
	{
		EventBase instance = (EventBase)template.Duplicate();
		instance.AttributeSet = (EventAttributeSet)AttributeSetCopier.DeepCopy(template.AttributeSet);
		AddChild(instance);
		return instance;
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

	// 回合开始：清空上回合事件并执行排程钩子，生成事件则广播事件组
	private void OnTurnStarted(int round, int turn)
	{
		ClearEvents();
		ScheduleEvents(round, turn);
		if (CurrentEvents.Count > 0)
		{
			EventsGeneratedEvent?.Invoke(round, turn, CurrentEvents);
		}
	}

	// 排程钩子：按回合规则决定本回合事件组。
	// 末回合（第 8 回合）生成 1 个 PvP 事件；第 4 回合生成 3 个怪物事件；其余回合生成 3 选事件组（至少 1 个商店事件）。
	protected virtual void ScheduleEvents(int round, int turn)
	{
		// 新对局首回合重置本局已出现事件记录
		if (round == 1 && turn == 1)
		{
			_seenEvents.Clear();
		}

		// 按轮次范围过滤可排布模板：MinRound ≤ round ≤ MaxRound
		List<EventBase> eligible = new();
		foreach (EventBase template in EventTemplates)
		{
			EventAttributeSet attr = template.AttributeSet;
			if (attr.MinRound.CurrentValue <= round && round <= attr.MaxRound.CurrentValue)
			{
				eligible.Add(template);
			}
		}

		if (turn == RoundTurnManager.TURNS_PER_ROUND)
		{
			CreatePvPEvent();
		}
		else if (turn == 4)
		{
			CreateMonsterEvents(eligible);
		}
		else
		{
			CreateDefaultEvents(eligible);
		}
	}

	// 创建 PvP 事件：复制模板；战斗对手由后续接线任务填充
	private void CreatePvPEvent()
	{
		foreach (EventBase template in EventTemplates)
		{
			if (template is PvPEvent)
			{
				AddCreated(template);
				return;
			}
		}
	}

	// 创建怪物事件组：固定 3 个怪物事件，每个挂载一个怪物实例；无怪物事件/怪物模板时退化为默认事件组
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

	// 创建默认 3 选事件组：先按权重选 1 个商店事件保证组内 ≥1 商店，再补足到 3 个；池不足时取全部可用
	private void CreateDefaultEvents(List<EventBase> eligible)
	{
		List<EventBase> pool = eligible.FindAll(evt => evt is not MonsterEvent && evt is not PvPEvent);
		if (pool.Count == 0)
		{
			return;
		}

		List<EventBase> remaining = new(pool);

		// 优先选 1 个商店事件（商店候选中按权重选取）
		List<EventBase> shopPool = remaining.FindAll(evt => evt is ShopEvent);
		if (shopPool.Count > 0)
		{
			EventBase shop = PickWeighted(shopPool);
			AddCreated(shop);
			remaining.Remove(shop);
		}

		// 其余从完整非怪物池中按权重补足（可再含商店）
		int target = Math.Min(3, pool.Count);
		while (CurrentEvents.Count < target && remaining.Count > 0)
		{
			EventBase picked = PickWeighted(remaining);
			AddCreated(picked);
			remaining.Remove(picked);
		}
	}

	// 复制模板创建事件实例、加入当前回合事件组并标记为本局已出现
	private void AddCreated(EventBase template)
	{
		EventBase evt = CreateEvent(template);
		AddEvent(evt);
		MarkSeen(evt);
	}

	// 按权重随机选取一个模板：权重 = 基础权重 ×（本局未出现则乘未出现倍率）
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

	// 计算模板的排布权重：未出现事件翻倍
	private float GetSelectionWeight(EventBase template)
	{
		float baseWeight = template.AttributeSet.Weight.CurrentValue;
		bool unseen = !_seenEvents.Contains(template.AttributeSet.EventKey);
		return baseWeight * (unseen ? UNSEEN_MULTIPLIER : 1f);
	}

	// 记录事件为本局已出现（按 EventKey）
	private void MarkSeen(EventBase evt)
	{
		_seenEvents.Add(evt.AttributeSet.EventKey);
	}

	// 反射收集所有非抽象公开的 EventBase 子类作为模板
	private void RegisterEventTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(EventBase).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is EventBase evt)
			{
				AddChild(evt);
				EventTemplates.Add(evt);
			}
		}
	}
}