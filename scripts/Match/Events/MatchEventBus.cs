using System;

namespace Project_Star.Match.Events;

// 局内事件总线：静态观察者通道，由管理器在事件发生时广播，供 UI / 卡牌被动订阅。
public static class MatchEventBus
{
	// 局内事件广播
	public static event Action<MatchEventBase>? EventRaised;

	// 广播局内事件
	public static void Raise(MatchEventBase evt)
	{
		EventRaised?.Invoke(evt);
	}
}
