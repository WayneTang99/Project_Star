using System;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

// 中毒施加分类与固定、属性、百分比基础量计算（领域战斗规则）。
public static class PoisonEffectRules
{
    // 仅匹配实际施加中毒的效果，不包含监听或光环。
    public static bool AppliesPoison(EffectDefinition effect) => effect is
        ApplyStatusEffectDefinition { Status: BattleStatus.Poison }
        or ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Poison }
        or ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Poison }
        or ApplyPrimaryEffectPercentStatusEffectDefinition { Status: BattleStatus.Poison };

    // 百分比先取整，永久加成在基础量之后统一累加一次。
    public static long BaseAmount(EffectDefinition effect, Func<StringName, long> value) => effect switch
    {
        ApplyStatusEffectDefinition { Status: BattleStatus.Poison } fixedAmount => fixedAmount.Amount,
        ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Poison } attribute => value(attribute.AttributeKey),
        ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Poison } percent
            => value(percent.AttributeKey) * percent.Percent / 100,
        _ => 0,
    };
}
