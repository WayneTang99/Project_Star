using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 生命值首次跌破半血事件。
public class HealthBelowHalfEvent : CombatEventBase
{
	// 目标
	public ICombatant Combatant { get; }

	public HealthBelowHalfEvent(ICombatant combatant)
	{
		Combatant = combatant;
	}
}