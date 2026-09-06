using System;

namespace Project_Star.Core.Interfaces;

// 局内被动能力接口：声明响应的局内事件类型（购买、出售、战斗胜利等），供分发器按类型路由。
public interface IMatchPassiveAbility
{
	// 响应的局内事件类型
	Type ReactEventType { get; }
}
