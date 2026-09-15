using Project_Star.Core.Events;

namespace Project_Star.Combat.Events;

// 战斗内事件总线（战斗层）：战斗逻辑通信与被动触发，与局外总线互不互通。
public sealed class CombatEventBus : EventBus<CombatEvent>
{
}