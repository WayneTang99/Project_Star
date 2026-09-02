using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Entities.Effects;

// 即时攻击力提升效果：应用一次立即增加目标卡牌的攻击力。
[GlobalClass]
public partial class AttackBoostEffect : AriaEffectBase
{
	// 攻击力提升数值
	[Export]
	public float BoostAmount { get; set; } = 5f;

	public AttackBoostEffect()
	{
		Key = new StringName("AttackBoost");
		DurationType = AriaEffectDurationType.Instant;
	}

	public override void Apply(AriaContextBase baseCtx)
	{
		var ctx = (BattleContext)baseCtx;
		var target = ctx.Target ?? ctx.FriendlyHero;
		if (target?.AttributeSet is not CardAttributeSet cardAttr)
		{
			return;
		}

		cardAttr.AttackPower.SetCurrentValue(cardAttr.AttackPower.CurrentValue + BoostAmount);
	}
}
