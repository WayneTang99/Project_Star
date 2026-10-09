namespace Project_Star.Domain.Combat;

// 识别实际施加疾速的效果，计算出售预检所需的最大单次时长（领域战斗规则）。
public static class HasteEffectRules
{
    public static bool AppliesHaste(EffectDefinition effect) => MaximumDurationTicks(effect, 0) is not null;

    // 加成先累加到基础时长，再应用效果自身的目标标签倍率；不含状态反应监听。
    public static long? MaximumDurationTicks(EffectDefinition effect, long bonusTicks) => effect switch
    {
        ApplyStatusEffectDefinition { Status: BattleStatus.HasteDuration } value => value.Amount + bonusTicks,
        ApplyStatusToRandomEnemyCardEffectDefinition { Status: BattleStatus.HasteDuration } value => value.Amount + bonusTicks,
        ApplyStatusToRandomAlliedCardEffectDefinition { Status: BattleStatus.HasteDuration } value => value.Amount + bonusTicks,
        ApplyStatusToAdjacentAlliedCardsEffectDefinition { Status: BattleStatus.HasteDuration } value
            => (value.Amount + bonusTicks) * value.BonusMultiplier,
        _ => null,
    };
}
