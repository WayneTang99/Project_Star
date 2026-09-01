using System;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Core.Managers;

// 事件管理器：回合开始生成事件组（排程钩子供覆写），维护当前回合事件与结算。
[GlobalClass]
public partial class EventManager : Node
{
	private RoundTurnManager _roundTurnManager = null!;

	// 全部事件模板（反射收集，每种一张）
	public Godot.Collections.Array<EventBase> EventTemplates { get; private set; } = new();

	// 当前回合的事件组
	public Godot.Collections.Array<EventBase> CurrentEvents { get; private set; } = new();

	// 怪物事件子管理器
	public MonsterEventManager MonsterEventManager { get; } = new();

	// 商店事件子管理器
	public ShopEventManager ShopEventManager { get; } = new();

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
		if (_roundTurnManager is not null)
		{
			_roundTurnManager.TurnStartedEvent -= OnTurnStarted;
		}
	}

	// 从模板复制一份事件实例并挂载为本节点子节点
	public EventBase CreateEvent(EventBase template)
	{
		EventBase instance = (EventBase)template.Duplicate();
		instance.AttributeSet = (EventAttributeSet)template.AttributeSet.Duplicate(true);
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

	// 排程钩子：决定本回合生成哪些事件（默认留空，由排程系统覆写）
	protected virtual void ScheduleEvents(int round, int turn)
	{
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