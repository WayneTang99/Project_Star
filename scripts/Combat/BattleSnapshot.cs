using System.Collections.Generic;
using Aria;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat;

// 战斗状态快照：保存所有参战实体的关键属性值，用于回滚。
public class BattleSnapshot
{
	// 快照时间步数
	public int StepNumber { get; }

	// 每个实体的属性快照（key = 实体哈希码，value = 属性名→当前值）
	public Dictionary<int, Dictionary<string, float>> EntityStates { get; } = new();

	// 实体引用映射（哈希码 → 实体）
	private readonly Dictionary<int, ICombatant> _entityMap = new();

	public BattleSnapshot(int stepNumber)
	{
		StepNumber = stepNumber;
	}

	// 保存指定实体的属性快照
	public void Capture(ICombatant? combatant)
	{
		if (combatant is null || combatant.AttributeSet is null)
		{
			return;
		}

		int id = combatant.GetHashCode();
		_entityMap[id] = combatant;

		var state = new Dictionary<string, float>();
		foreach (var kvp in combatant.AttributeSet.Attributes)
		{
			state[kvp.Key] = kvp.Value.CurrentValue;
		}

		EntityStates[id] = state;
	}

	// 从快照恢复指定实体的属性值
	public void Restore(ICombatant? combatant)
	{
		if (combatant is null || combatant.AttributeSet is null)
		{
			return;
		}

		int id = combatant.GetHashCode();
		if (!EntityStates.TryGetValue(id, out Dictionary<string, float>? state))
		{
			return;
		}

		foreach (var kvp in state)
		{
			AriaAttributeData? attr = combatant.AttributeSet.GetAttribute(kvp.Key);
			if (attr is not null)
			{
				attr.SetCurrentValue(kvp.Value);
			}
		}
	}

	// 恢复所有实体到快照状态
	public void RestoreAll()
	{
		foreach (var kvp in EntityStates)
		{
			if (_entityMap.TryGetValue(kvp.Key, out ICombatant? combatant))
			{
				Restore(combatant);
			}
		}
	}
}
