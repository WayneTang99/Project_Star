using System;
using System.Collections.Generic;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat;

// 数据驱动能力辅助工具：为 DataDrivenAbility 注入目标解析与效果创建委托。
// 保持 Aria 插件游戏无关，游戏侧的上下文解析与效果映射逻辑集中在此。
public static class AbilityHelper
{
	// 根据 AbilityDefinition 创建已配置目标解析和效果创建的 DataDrivenAbility
	public static DataDrivenAbility Create(AbilityDefinition definition)
	{
		var ability = new DataDrivenAbility(definition)
		{
			TargetResolver = CreateTargetResolver(definition.TargetType),
			EffectFactory = EffectFactory.Create,
		};
		return ability;
	}

	// 根据 TargetType 生成对应的目标解析委托
	private static Func<AriaContextBase, IAriaEntity?> CreateTargetResolver(AbilityTargetType targetType)
	{
		return targetType switch
		{
			AbilityTargetType.EnemyHero => ctx =>
			{
				if (ctx is BattleContext b) return b.EnemyHero;
				return null;
			},
			AbilityTargetType.FriendlyHero => ctx =>
			{
				if (ctx is BattleContext b) return b.FriendlyHero;
				return null;
			},
			AbilityTargetType.Self => ctx =>
			{
				if (ctx is BattleContext b) return b.Self as IAriaEntity;
				return null;
			},
			AbilityTargetType.RandomEnemyCard => ctx =>
			{
				if (ctx is BattleContext b) return PickRandom(b.EnemyCards);
				return null;
			},
			AbilityTargetType.AllEnemies => ctx => null,
			AbilityTargetType.AllFriendlies => ctx => null,
			_ => _ => null,
		};
	}

	// 从列表中随机选取一个
	private static IAriaEntity? PickRandom(List<ICombatant> list)
	{
		if (list.Count == 0) return null;
		return list[(int)(GD.Randi() % (ulong)list.Count)];
	}
}
