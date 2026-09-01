using Godot;
using Project_Star.Core.AttributeSets;

namespace Project_Star.Core.Bases;

// 卡牌抽象基类，持有一份卡牌属性集。
[GlobalClass]
public abstract partial class CardBase : Node
{
	// 卡牌属性集
	public CardAttributeSet AttributeSet { get; set; } = null!;
}