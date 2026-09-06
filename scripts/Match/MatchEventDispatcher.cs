using System;
using System.Collections.Generic;
using Aria;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;
using Project_Star.Match.Events;

namespace Project_Star.Match;

// 局内事件分发器：订阅 MatchEventBus，按 IMatchPassiveAbility.ReactEventType 路由被动能力。
public class MatchEventDispatcher
{
	private readonly Dictionary<Type, List<PassiveBinding>> _passiveIndex = new();

	public MatchEventDispatcher()
	{
		MatchEventBus.EventRaised += OnEventRaised;
	}

	// 注销事件总线
	public void Dispose()
	{
		MatchEventBus.EventRaised -= OnEventRaised;
	}

	// 注册实体的局内被动能力
	public void Register(ICombatant? combatant)
	{
		if (combatant is null) return;
		foreach (AriaAbilityBase ability in combatant.Abilities)
		{
			if (ability is IMatchPassiveAbility passive)
			{
				if (!_passiveIndex.TryGetValue(passive.ReactEventType, out List<PassiveBinding>? list))
				{
					list = new List<PassiveBinding>();
					_passiveIndex[passive.ReactEventType] = list;
				}
				list.Add(new PassiveBinding(ability, combatant));
			}
		}
	}

	// 注销实体的所有被动能力
	public void Unregister(ICombatant? combatant)
	{
		if (combatant is null) return;
		foreach (var list in _passiveIndex.Values)
		{
			list.RemoveAll(b => ReferenceEquals(b.Combatant, combatant));
		}
	}

	// 清空所有注册
	public void Clear()
	{
		_passiveIndex.Clear();
	}

	// 收到局内事件后路由
	private void OnEventRaised(MatchEventBase evt)
	{
		Type eventType = evt.GetType();
		if (!_passiveIndex.TryGetValue(eventType, out List<PassiveBinding>? passives)) return;

		foreach (PassiveBinding passive in passives)
		{
			if (!passive.Ability.CanActivate(BuildContext(passive.Combatant, evt))) continue;

			foreach (AriaAction action in passive.Ability.Activate(BuildContext(passive.Combatant, evt)))
			{
				foreach ((IAriaEntity target, AriaEffectBase[] effects) in action.EffectsByTarget)
				{
					foreach (AriaEffectBase effect in effects)
					{
						var ctx = new BattleContext { Source = passive.Combatant, Target = target as ICombatant };
						effect.Apply(ctx);
					}
				}
			}
		}
	}

	private static BattleContext BuildContext(ICombatant? source, MatchEventBase evt)
	{
		return new BattleContext { Source = source, Self = source };
	}

	private readonly struct PassiveBinding
	{
		public AriaAbilityBase Ability { get; }
		public ICombatant Combatant { get; }

		public PassiveBinding(AriaAbilityBase ability, ICombatant combatant)
		{
			Ability = ability;
			Combatant = combatant;
		}
	}
}
