using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Core.Bases;

// 卡牌抽象基类，持有一份卡牌属性集。
[GlobalClass]
public abstract partial class CardBase : Node, ICombatant
{
	// 卡牌属性集
	public CardAttributeSet AttributeSet { get; set; } = null!;

	// 能力（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaAbilityBase> Abilities { get; } = new();

	// 效果（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaEffectBase> Effects { get; } = new();

	// IAriaEntity 显式实现：以基类型暴露属性集
	AriaAttributeSet IAriaEntity.AttributeSet => AttributeSet;
}