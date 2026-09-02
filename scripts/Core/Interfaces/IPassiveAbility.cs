using System;

namespace Project_Star.Core.Interfaces;

// 被动能力接口：声明响应的事件类型，供战斗管理器按类型路由。
public interface IPassiveAbility
{
	// 响应的事件类型
	Type ReactEventType { get; }
}