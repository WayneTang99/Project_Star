using System;
using System.Collections.Generic;

namespace Project_Star.Core.Events;

// 事件总线（共享层）：强类型事件按 Type 分发，订阅返回退订令牌，发布沿继承链分发给基类处理器。
public class EventBus<TEvent> where TEvent : EventBase
{
    private readonly Dictionary<Type, List<Action<EventBase>>> _handlers = new();

    // 订阅事件；返回令牌，Dispose 即退订。
    public IDisposable Subscribe<T>(Action<T> handler) where T : TEvent
    {
        var wrapped = new Action<EventBase>(evt => handler((T)evt));
        var list = GetList(typeof(T));
        list.Add(wrapped);
        return new Subscription(() => list.Remove(wrapped));
    }

    // 发布事件；沿继承链分发给已订阅的基类处理器。
    public void Publish<T>(T evt) where T : TEvent
    {
        for (var type = evt.GetType(); type != null && type != typeof(EventBase); type = type.BaseType)
        {
            if (_handlers.TryGetValue(type, out var list))
            {
                var snapshot = list.ToArray();
                foreach (var handler in snapshot)
                    handler(evt);
            }
        }
    }

    private List<Action<EventBase>> GetList(Type type)
    {
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = new List<Action<EventBase>>();
            _handlers[type] = list;
        }
        return list;
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}