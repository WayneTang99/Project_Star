using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 麻痹能力：对敌方随机一张卡施加麻痹效果，CD 读取进度减半。
[GlobalClass]
public partial class ParalysisAbility : AriaAbilityBase
{
	// 持续时长（秒）
	[Export]
	public float DurationSeconds { get; set; } = 5f;

	public ParalysisAbility()
	{
		Key = new StringName("Paralysis");
		DisplayName = "麻痹";
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
				[target] = [new ParalysisEffect { Duration = DurationSeconds }]
			}
		}];
	}
}
