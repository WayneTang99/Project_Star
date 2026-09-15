using Godot;

namespace Project_Star.Core.Abilities;

// 效果抽象基类（核心层）：实现 IEffect，提供周期效果所需的时长/周期/贡献量字段。
// 即时效果 Duration = 0、TickInterval = 0；周期效果由战斗解析器按时钟驱动。
// 堆叠约定（Apply 累加 / 到期扣减 / 不清零 / 抵消判定）由战斗层管理，基类不实现。
public abstract class EffectBase : IEffect
{
    public required StringName EffectKey { get; init; }
    public required string DisplayName { get; init; }
    public EffectType Type { get; init; }

    // 剩余持续时长（即时效果为 0，周期效果由战斗解析器递减）
    public int Duration { get; set; }
    // 触发周期（即时效果为 0，周期效果按此间隔结算，单位为逻辑步）
    public int TickInterval { get; set; }
    // 本次施加贡献量（用于到期扣减，Apply 累加到目标属性的量）
    public int ContributionAmount { get; set; }

    // 施加效果到目标（子类实现具体逻辑）。
    public abstract void Apply(object context);
}
