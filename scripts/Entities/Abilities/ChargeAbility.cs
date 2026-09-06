using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 充能能力：立即减少自身所有能力的冷却秒数。通用可复用。
[GlobalClass]
public partial class ChargeAbility : AriaAbilityBase
{
	// 减少的冷却秒数
	[Export]
	public float ReductionSeconds { get; set; } = 3f;

	public ChargeAbility()
	{
		Key = new StringName("Charge");
		DisplayName = "充能";
		CooldownSeconds = 6f;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		if (ctx.Source is null) return [];
		return [new AriaAction
		{
			EffectsByTarget =
			{
				[ctx.Source] = [new CooldownReductionEffect { ReductionSeconds = ReductionSeconds }]
			}
		}];
	}
}
