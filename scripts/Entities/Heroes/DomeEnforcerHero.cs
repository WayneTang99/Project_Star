using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Heroes;

// 穹顶执法官：高生命、低财富的防御型英雄。
public partial class DomeEnforcerHero : HeroBase
{
	public DomeEnforcerHero()
	{
		AttributeSet = new HeroAttributeSet(new StringName("Dome_Enforcer"), "穹顶执法官");
	}

	protected override void ApplyInitialAttributes()
	{
		AttributeSet.Health.SetCurrentValue(300f);
		AttributeSet.Armor.SetCurrentValue(0f);
		AttributeSet.Wealth.SetCurrentValue(15f);
		AttributeSet.Reputation.SetCurrentValue(20f);
	}
}
