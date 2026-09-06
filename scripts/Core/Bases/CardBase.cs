using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Core.Bases;

// 卡牌抽象基类，持有一份卡牌属性集与标签集。
[GlobalClass]
public abstract partial class CardBase : Node, ICombatant
{
	// 卡牌属性集
	public CardAttributeSet AttributeSet { get; set; } = null!;

	// 标签集（词条）
	public AriaTagSet TagSet { get; set; } = new();

	// 能力（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaAbilityBase> Abilities { get; } = new();

	// 效果（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaEffectBase> Effects { get; } = new();

	// 卡牌冷却时长（秒），构造时设置；CombatManager 据此重置冷却
	public float CooldownDuration { get; set; }

	// 等级变化钩子，子类覆写以根据等级更新属性值
	protected virtual void OnLevelChanged(int newLevel) { }

	// 订阅等级变化事件，子类构造函数末尾调用
	protected void InitializeLevelListener()
	{
		AttributeSet.Level.OnValueChanged += (_, _) =>
			OnLevelChanged((int)AttributeSet.Level.CurrentValue);
		// 补一次初始调用：构造时 Level 已赋值但不触发事件，确保 ATTACK_POWER 等属性被初始化
		OnLevelChanged((int)AttributeSet.Level.CurrentValue);
	}

	// IAriaEntity 显式实现：以基类型暴露属性集
	AriaAttributeSet IAriaEntity.AttributeSet => AttributeSet;
}