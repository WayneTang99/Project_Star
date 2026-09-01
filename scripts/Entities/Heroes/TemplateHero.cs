using Godot;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Heroes;

// 模板英雄：示例英雄，覆写初始属性。
public partial class TemplateHero : HeroBase
{
	public TemplateHero()
	{
		AttributeSet = new HeroAttributeSet(new StringName("Template"), "模板英雄", new StringName("Template"));
	}

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