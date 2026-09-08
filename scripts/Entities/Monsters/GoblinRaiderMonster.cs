using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Entities.Cards;

namespace Project_Star.Entities.Monsters;

// 哥布林劫掠者：示例怪物，继承 MonsterBase，持有属性集与一叠卡牌。
public partial class GoblinRaiderMonster : MonsterBase
{
	public GoblinRaiderMonster()
	{
		AttributeSet = new HeroAttributeSet(new StringName("Goblin_Raider"), "哥布林劫掠者");
	}

	protected override void ApplyInitialAttributes()
	{
		AttributeSet.Health.SetCurrentValue(120f);
		AttributeSet.Armor.SetCurrentValue(5f);
		AttributeSet.Wealth.SetCurrentValue(0f);
		AttributeSet.Reputation.SetCurrentValue(100f);
	}

	public override void _Ready()
	{
		base._Ready();
		Deck.Add(new ShockPistolCard());
		Deck.Add(new ShockPistolCard());
	}
}
