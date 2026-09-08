using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Tests;

// 快速测试英雄：极低生命保证战斗秒结束，使对局闭环在限时内闭合。
// internal 声明：反射模板池按 type.IsPublic 过滤，不会污染生产英雄池（AvailableHeroes）。
internal sealed partial class FastTestHero : HeroBase
{
	public FastTestHero()
	{
		AttributeSet = new HeroAttributeSet(new StringName("Fast_Test"), "快速测试英雄");
	}

	protected override void ApplyInitialAttributes()
	{
		// 生命设为 10：敌方战斗开始被动（10 点）即可秒杀，
		// 即使敌方幽灵只抽到纯治疗卡也能快速分出胜负，杜绝战斗僵局。
		AttributeSet.Health.SetCurrentValue(10f);
		AttributeSet.Wealth.SetCurrentValue(200f);
		AttributeSet.Experience.SetCurrentValue(0f);
		AttributeSet.Level.SetCurrentValue(1f);
		AttributeSet.Reputation.SetCurrentValue(20f);
	}
}