using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Events;

// 怪物事件：怪物对战事件，直接引用怪物实体（怪物继承 HeroBase，与英雄同样持有属性集与卡牌）。
public partial class MonsterEvent : EventBase
{
	// 怪物实体
	[Export]
	public HeroBase Monster { get; set; } = null!;

	public MonsterEvent()
	{
		AttributeSet = new EventAttributeSet(new StringName("Monster_Event"), "怪物事件");
	}
}