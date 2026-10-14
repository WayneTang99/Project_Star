using System;

namespace Project_Star.Domain.Combat;

// 统一计算主动卡牌的最终冷却，加减和倍率后最低一秒（领域战斗层）。
public static class BattleCooldownRules
{
    // 返回半Tick单位的完整冷却；充能与倒计时仍可将剩余时长减至零。
    public static decimal DurationUnits(int cooldownTicks, int bonusTicks = 0, decimal multiplier = 1m) =>
        Math.Max(20m, checked((decimal)cooldownTicks + bonusTicks) * 2m * multiplier);
}
