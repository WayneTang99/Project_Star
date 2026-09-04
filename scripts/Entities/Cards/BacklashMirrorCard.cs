using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Abilities;

namespace Project_Star.Entities.Cards;

// 反噬之镜：每当敌方使用卡牌时，对敌方造成10点伤害。
public partial class BacklashMirrorCard : CardBase
{
	public BacklashMirrorCard()
	{
		AttributeSet = new CardAttributeSet(
			new StringName("Backlash_Mirror"),
			"反噬之镜",
			new StringName("Neutral"),
			CardSize.Small);
		TagSet.Add(Tags.FromSize(AttributeSet.Size));
		Abilities.Add(new BacklashPassiveAbility { DamageAmount = 10f });
	}
}
