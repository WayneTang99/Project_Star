using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;
using Project_Star.Core.Types;

namespace Project_Star.Core.Bases;

// 卡牌抽象基类，持有一份卡牌属性集与标签集。
[GlobalClass]
public abstract partial class CardBase : Node, ICombatant, IEntity
{
	// 卡牌属性集
	public CardAttributeSet AttributeSet { get; set; } = null!;

	// IEntity 显式实现
	AriaAttributeSet? IEntity.AttributeSet => AttributeSet;

	// 标签集（词条）
	public AriaTagSet TagSet { get; set; } = new();

	// 能力（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaAbilityBase> Abilities { get; } = new();

	// 效果（声明，构造函数或子类填充）
	public Godot.Collections.Array<AriaEffectBase> Effects { get; } = new();

	// 卡牌冷却时长（秒），构造时设置；CombatManager 据此重置冷却
	public float CooldownDuration { get; set; }

	// 价值系数：初始价值 = 系数 * 等级 * 型号系数；普通卡默认 2，特殊价值物品覆写（如兽皮 4）
	protected virtual float ValueScale => CardEconomy.STANDARD_VALUE_SCALE;

	// 计算当前初始价值（价值系数 * 当前等级 * 型号系数）
	public float GetInitialValue() =>
		CardEconomy.InitialValue(ValueScale, (int)AttributeSet.Level.CurrentValue, AttributeSet.Size);

	// 将价值属性同步为当前初始价值；子类构造函数在属性集就绪后调用
	protected void InitializeValue() =>
		AttributeSet.Value.SetCurrentValue(GetInitialValue());

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