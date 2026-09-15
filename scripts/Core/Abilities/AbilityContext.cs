using Project_Star.Core.States;

namespace Project_Star.Core.Abilities;

// 能力执行上下文（核心层）：封装能力执行所需的所有信息，避免参数列表膨胀。
// 新增上下文字段不改接口签名，已实现的能力不受影响（开闭原则）。
public class AbilityContext
{
    // 施法者英雄战斗状态（主动能力 = 发动方，被动能力 = 拥有者）
    public required HeroBattleState Caster { get; init; }
    // 目标英雄战斗状态（默认为敌方英雄，效果可覆写）
    public HeroBattleState? Target { get; set; }
    // 拥有此能力的卡牌战斗状态（主动能力填入，被动能力可能为 null）
    public CardBattleState? OwnerCard { get; set; }
    // 触发事件引用（被动能力由事件触发时填入，主动能力为 null；类型为 object 避免 Core 层依赖 Combat 层）
    public object? TriggerEvent { get; set; }
    // 棋盘查询接口（由战斗层注入实现，按需填入）
    public IBoardQuery? Board { get; set; }
}
