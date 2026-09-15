using Godot;

namespace Project_Star.Core.Abilities;

// 效果接口（核心层）：被能力触发的被动后果（即时结算 / 周期 DoT-HoT）。
// 效果由能力产生，战斗解析器按类型驱动结算；堆叠/到期扣减由战斗层管理。
public interface IEffect
{
    // 唯一标识
    StringName EffectKey { get; }
    // 展示名
    string DisplayName { get; }
    // 效果类型（即时/周期）
    EffectType Type { get; }
    // 施加效果到目标（通过 context 获取施法者/目标/战场等信息）
    void Apply(object context);
}
