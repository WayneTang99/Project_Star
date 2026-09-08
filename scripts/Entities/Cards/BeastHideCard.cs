using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Entities.Cards;

// 兽皮：无能力的战利品卡，价值系数 4（初始价值 = 4 * 等级 * 型号）。
// 归属中立 key（Neutral），不匹配任何英雄 → 不会出现在按英雄过滤的商店货架；
// 作为怪物掉落/奖励道具，加入玩家卡池后出售换取财富。
public partial class BeastHideCard : CardBase
{
	// 价值系数覆写：4（普通卡为 2）
	protected override float ValueScale => 4f;

	public BeastHideCard()
	{
		AttributeSet = new CardAttributeSet(new StringName("Beast_Hide"), "兽皮", new StringName("Neutral"), CardSize.Small);
		TagSet.Add(Tags.FromSize(AttributeSet.Size));
		InitializeValue();
	}
}
