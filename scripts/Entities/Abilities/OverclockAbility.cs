using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 超频能力：对敌方随机一张卡施加超频效果，CD 读取进度翻倍。
[GlobalClass]
public partial class OverclockAbility : AriaAbilityBase
{
	// 持续时长（秒）
	[Export]
	public float DurationSeconds { get; set; } = 5f;

	public OverclockAbility()
	{
		Key = new StringName("Overclock");
		DisplayName = "超频";
		CooldownSeconds = 8f;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		if (ctx.EnemyCards.Count == 0) return [];
		ICombatant target = ctx.EnemyCards[(int)(GD.Randi() % (ulong)ctx.EnemyCards.Count)];
		return [new AriaAction
		{
			EffectsByTarget =
			{
				[target] = [new OverclockEffect { Duration = DurationSeconds }]
			}
		}];
	}
}
