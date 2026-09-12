using System;
using Godot;

namespace Aria;

// 数据驱动能力桥接类：将 AbilityDefinition 包装为 AriaAbilityBase，
// 使数据驱动定义可直接用于现有战斗系统（Resolver/CombatManager）。
[GlobalClass]
public partial class DataDrivenAbility : AriaAbilityBase
{
	// 数据驱动定义
	public AbilityDefinition Definition { get; set; } = null!;

	// 目标解析委托（由管理器注入，用于运行时解析目标实体）
	public Func<IAriaEntity?, IAriaEntity?>? TargetResolver { get; set; }

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
		if (Definition is null) return [];

		// 解析目标：优先用注入的 TargetResolver，回退到 Owner（自身）
		IAriaEntity? target = TargetResolver?.Invoke(Owner) ?? Owner;

		var action = new AriaAction();

		if (target is not null && Definition.Effects.Count > 0)
		{
			var effects = new AriaEffectBase[Definition.Effects.Count];
			for (int i = 0; i < Definition.Effects.Count; i++)
			{
				effects[i] = EffectFactory.Create(Definition.Effects[i]);
			}
			action.EffectsByTarget[target] = effects;
		}

		return [action];
	}
}
