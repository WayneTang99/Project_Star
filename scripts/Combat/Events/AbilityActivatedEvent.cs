using Aria;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 能力发动事件：某能力被发动（主动或被动）。
public class AbilityActivatedEvent : CombatEventBase
{
	// 发动实体
	public ICombatant? Combatant { get; }

	// 发动的能力
	public AriaAbilityBase Ability { get; }

	public AbilityActivatedEvent(ICombatant? combatant, AriaAbilityBase ability)
	{
		Combatant = combatant;
		Ability = ability;
	}
}