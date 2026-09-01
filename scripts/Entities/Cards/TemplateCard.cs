using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Entities.Cards;

// 模板卡牌：示例卡牌。
public partial class TemplateCard : CardBase
{
	public TemplateCard()
	{
		AttributeSet = new CardAttributeSet(new StringName("Template_Card"), "模板卡牌", new StringName("Template"), CardSize.Small);
	}
}