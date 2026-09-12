using Aria;
using Godot;

namespace Project_Star.Core.Bases;

// 实体公共接口：所有游戏实体（英雄、卡牌、怪物、事件）共享的最小契约。
// 提供属性集访问，供池系统、管理器等通用逻辑使用。
public interface IEntity
{
	// 属性集（每个实体持有自己的 AriaAttributeSet 子类）
	AriaAttributeSet? AttributeSet { get; }
}
