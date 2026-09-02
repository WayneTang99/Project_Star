using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.TestUI;

// 战斗测试卡牌（棋盘棋子）：继承 CardBase（真实游戏实体基类），无生命值，仅提供能力。
internal sealed partial class CombatTestCard : CardBase
{
	// 构造：指定名称与能力列表
	public CombatTestCard(string name, params AriaAbilityBase[] abilities)
	{
		AttributeSet = new CardAttributeSet(new StringName(name), name, new StringName("Test"), CardSize.Small);
		foreach (AriaAbilityBase ability in abilities)
		{
			Abilities.Add(ability);
		}
	}
}