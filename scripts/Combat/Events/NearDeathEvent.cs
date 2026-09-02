using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 濒临死亡事件：生命值首次跌破濒死阈值（如 25%）时分发。
public class NearDeathEvent : CombatEventBase
{
	// 目标
	public ICombatant Combatant { get; }

	public NearDeathEvent(ICombatant combatant)
	{
		Combatant = combatant;
	}
}