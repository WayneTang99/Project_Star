using System;
using Godot;

namespace Aria;

// 数据驱动能力桥接类：将 AbilityDefinition 包装为 AriaAbilityBase，
// 使数据驱动定义可直接用于现有战斗系统（Resolver/CombatManager）。
// 目标选择与效果创建均通过委托注入，保持 Aria 插件游戏无关。
[GlobalClass]
public partial class DataDrivenAbility : AriaAbilityBase
{
	// 数据驱动定义
	public AbilityDefinition Definition { get; set; } = null!;

	// 目标解析委托：接收上下文，返回目标实体（由游戏侧注入）
	public Func<AriaContextBase, IAriaEntity?>? TargetResolver { get; set; }

	// 效果创建委托：从 EffectDefinition 创建具体效果实例（由游戏侧注入）
	public Func<EffectDefinition, AriaEffectBase>? EffectFactory { get; set; }

	public DataDrivenAbility()
	{
	}

	public DataDrivenAbility(AbilityDefinition definition)
	{
		Definition = definition;
		Key = definition.Key;
		DisplayName = definition.DisplayName;
		CooldownSeconds = definition.CooldownSeconds;
	}

	public override bool CanActivate(AriaContextBase ctx)
	{
		if (Definition?.ActivationCondition is null) return true;
		return Definition.ActivationCondition.Evaluate(Owner);
	}

	public override AriaAction[] Activate(AriaContextBase ctx)
	{
		if (Definition is null || EffectFactory is null) return [];

		// 解析目标：委托为 null 时回退到 Owner（自身）
		IAriaEntity? target = TargetResolver?.Invoke(ctx) ?? Owner as IAriaEntity;

		var action = new AriaAction();

		if (target is not null && Definition.Effects.Count > 0)
		{
			var effects = new AriaEffectBase[Definition.Effects.Count];
			for (int i = 0; i < Definition.Effects.Count; i++)
			{
				effects[i] = EffectFactory(Definition.Effects[i]);
			}
			action.EffectsByTarget[target] = effects;
		}

		return [action];
	}
}
