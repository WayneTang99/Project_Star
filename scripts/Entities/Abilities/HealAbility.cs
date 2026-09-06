using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Entities.Abilities;

// 治疗能力：恢复己方英雄（FriendlyHero）生命（不超过最大生命）。
// 治疗量优先从持有者卡牌的 HEAL_POWER 属性读取，无属性时回退到 HealAmount。
[GlobalClass]
public partial class HealAbility : AriaAbilityBase
{
	// 治疗数值（回退值，当持有者无 HEAL_POWER 属性时使用）
	[Export]
	public float HealAmount { get; set; } = 15f;

	public HealAbility() { }

	public HealAbility(float cooldownSeconds)
	{
		CooldownSeconds = cooldownSeconds;
	}

	public override float GetCooldownSeconds()
	{
		if (Owner is CardBase card && card.AttributeSet.GetAttribute(CardAttributeSet.COOLDOWN) is { } cooldown)
		{
			return cooldown.CurrentValue;
		}
		return CooldownSeconds;
	}

	public override AriaAction[] Activate(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		ICombatant? target = ctx.FriendlyHero;
		if (target is null)
		{
			return [];
		}

		float heal = HealAmount;
		if (Owner is CardBase card && card.AttributeSet.GetAttribute(CardAttributeSet.HEAL_POWER) is { } healPower)
		{
			heal = healPower.CurrentValue;
		}

		return [ new AriaAction { EffectsByTarget = { [target] = [ new HealEffect { HealAmount = heal } ] } } ];
	}
}
