using Project_Star.Core.Events;

namespace Project_Star.Combat.Events;

// 战斗事件抽象基类（战斗层）：战斗内总线消息的根类型，与局外事件（MatchEvent）分离。
public abstract class CombatEvent : EventBase
{
}