using Project_Star.Core.Bases;

namespace Project_Star.Entities.Heroes;

public partial class TemplateHero : HeroBase
{
	public override string HeroName => "Template";

	protected override void ApplyInitialAttributes()
	{
		AttributeSet.Health.SetCurrentValue(150f);
		AttributeSet.Armor.SetCurrentValue(10f);
		AttributeSet.Wealth.SetCurrentValue(200f);
		AttributeSet.Experience.SetCurrentValue(0f);
		AttributeSet.Level.SetCurrentValue(1f);
		AttributeSet.Reputation.SetCurrentValue(100f);
	}
}