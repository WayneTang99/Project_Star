using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.TestUI;

// 测试用卡牌（internal，不被 CardManager 反射收集）。
internal sealed partial class BoardTestCard : CardBase
{
	public BoardTestCard(CardSize size)
	{
		AttributeSet = new CardAttributeSet(new StringName($"Test_{size}"), $"测试{size}", new StringName("Test"), size);
	}
}