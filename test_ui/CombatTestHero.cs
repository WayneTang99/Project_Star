using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.TestUI;

// 战斗测试英雄：继承 HeroBase（真实游戏实体基类），持有英雄属性集与能力。
internal sealed partial class CombatTestHero : HeroBase
{
	// 构造：指定名称 / 生命 / 护甲与能力列表
	public CombatTestHero(string name, float maxHealth, float armor, params AriaAbilityBase[] abilities)
	{
		AttributeSet = new HeroAttributeSet(new StringName(name), name);
		AttributeSet.MaxHealth.SetCurrentValue(maxHealth);
		AttributeSet.Health.SetCurrentValue(maxHealth);
		AttributeSet.Armor.SetCurrentValue(armor);
		foreach (AriaAbilityBase ability in abilities)
		{
			Abilities.Add(ability);
		}
	}

	// 当前生命
	public float Health => AttributeSet.Health.CurrentValue;

	// 最大生命
	public float MaxHealth => AttributeSet.MaxHealth.CurrentValue;

	// 当前护甲
	public float Armor => AttributeSet.Armor.CurrentValue;

	// 是否存活
	public bool IsAlive => Health > 0f;
}