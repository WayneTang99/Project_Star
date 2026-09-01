using Aria;
using Godot;

namespace Project_Star.Core.Interfaces;

// 战斗参与者接口：英雄/卡牌共同实现，声明能力与效果。
public interface ICombatant : IAriaEntity
{
	// 能力（声明）
	Godot.Collections.Array<AriaAbilityBase> Abilities { get; }

	// 效果（声明）
	Godot.Collections.Array<AriaEffectBase> Effects { get; }
}
