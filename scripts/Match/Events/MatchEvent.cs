using Project_Star.Core.Events;

namespace Project_Star.Match.Events;

// 局外事件抽象基类（对局层）：对局内总线消息的根类型，与战斗事件（CombatEvent）分离。
public abstract class MatchEvent : EventBase
{
}