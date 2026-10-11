using System;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

// 灼伤施加分类与固定、属性、百分比基础量计算（领域战斗规则）。
public static class BurnEffectRules
{
    // 仅匹配实际施加灼伤的效果，不包含监听或光环。
    public static bool AppliesBurn(EffectDefinition effect) => effect is
        ApplyStatusEffectDefinition { Status: BattleStatus.Burn }
        or ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn }
        or ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Burn }
        or ApplyPrimaryEffectPercentStatusEffectDefinition { Status: BattleStatus.Burn };

    // 百分比先取整，永久加成在基础量之后统一累加一次。
    public static long BaseAmount(EffectDefinition effect, Func<StringName, long> value) => effect switch
    {
        ApplyStatusEffectDefinition { Status: BattleStatus.Burn } fixedAmount => fixedAmount.Amount,
        ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn } attribute => value(attribute.AttributeKey),
        ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Burn } percent
            => value(percent.AttributeKey) * percent.Percent / 100,
        _ => 0,
    };
}
