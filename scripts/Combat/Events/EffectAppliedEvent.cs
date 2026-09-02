using Aria;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 效果结算事件：某效果已结算完成。
public class EffectAppliedEvent : CombatEventBase
{
	// 效果目标
	public ICombatant? Target { get; }

	// 已结算的效果
	public AriaEffectBase Effect { get; }

	public EffectAppliedEvent(ICombatant? target, AriaEffectBase effect)
	{
		Target = target;
		Effect = effect;
	}
}