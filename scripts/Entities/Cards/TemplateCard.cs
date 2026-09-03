using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Abilities;

namespace Project_Star.Entities.Cards;

// 模板卡牌：示例卡牌。
public partial class TemplateCard : CardBase
{
	public TemplateCard()
	{
		AttributeSet = new CardAttributeSet(new StringName("Template_Card"), "模板卡牌", new StringName("Template"), CardSize.Small);
		Abilities.Add(new PassiveDamageAbility { DamageAmount = 10f, EventType = CombatEventType.BattleStart });
		Abilities.Add(new HealAbility(10f) { HealAmount = 10f });
	}
}