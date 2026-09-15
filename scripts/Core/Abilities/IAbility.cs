using Godot;

namespace Project_Star.Core.Abilities;

// 能力接口（核心层）：可被触发的动作单元。主动与被动都是能力，区别仅在于触发方式。
// 能力只负责执行逻辑；触发、冷却、编排、连锁统一由战斗解析器负责。
// 能力必须可复用，禁止为单张卡写专属能力。
public interface IAbility
{
    // 唯一标识
    StringName AbilityKey { get; }
    // 展示名
    string DisplayName { get; }
    // 触发方式（主动/被动）
    AbilityTrigger Trigger { get; }
    // 能量消耗（0 = 免能量）
    int EnergyCost { get; }
    // 基础冷却时间
    int Cooldown { get; }
    // 是否满足发动条件（子类可覆写扩展）
    bool CanActivate(object caster);
    // 执行能力逻辑（context 为执行上下文，2.3 步实现）
    void Execute(object context);
}
