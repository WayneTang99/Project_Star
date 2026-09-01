using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Bases;

// 事件抽象基类：持有一份事件属性集，结算逻辑由子类覆写。
[GlobalClass]
public abstract partial class EventBase : Node
{
	// 事件属性集
	[Export]
	public EventAttributeSet AttributeSet { get; set; } = null!;

	// 事件状态；由 EventManager 流转
	public EventState State { get; private set; } = EventState.Pending;

	public override void _Ready()
	{
		base._Ready();
		Initialize();
	}

	// 初始化事件内容，供子类覆写
	protected virtual void Initialize()
	{
	}

	// 结算事件：置为 Resolved 并执行子类结算逻辑
	public void Resolve()
	{
		if (State != EventState.Pending)
		{
			return;
		}

		OnResolve();
		State = EventState.Resolved;
	}

	// 事件结算逻辑，供子类覆写
	protected virtual void OnResolve()
	{
	}
}