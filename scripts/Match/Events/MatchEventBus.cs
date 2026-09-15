using Project_Star.Core.Events;

namespace Project_Star.Match.Events;

// 局外事件总线（对局层）：对局内管理器间通信，与战斗内总线互不互通。
public sealed class MatchEventBus : EventBus<MatchEvent>
{
}