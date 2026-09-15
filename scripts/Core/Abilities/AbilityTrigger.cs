namespace Project_Star.Core.Abilities;

// 能力触发方式枚举（核心层）：区分主动与被动，由战斗解析器决定触发时机。
public enum AbilityTrigger
{
    // 主动：冷却到期后自动发动
    Active,
    // 被动：监听战斗事件触发
    Passive,
}
