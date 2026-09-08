namespace Project_Star.Core.Bases;

// 怪物抽象基类：继承英雄基类，与英雄同样持有属性集与卡牌；具体怪物为其叶子子类。
public abstract partial class MonsterBase : HeroBase
{
	// 怪物卡组（牌堆），构造函数或子类填充
	public Godot.Collections.Array<CardBase> Deck { get; } = new();
}
