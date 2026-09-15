namespace Project_Star.Core.Abilities;

// 效果类型枚举（核心层）：区分即时结算与周期结算，由战斗解析器按类型驱动。
public enum EffectType
{
    // 即时：施加后立即结算（伤害/治疗/护甲）
    Instant,
    // 周期：按时长与周期持续结算（辐射/腐蚀/超频等）
    Periodic,
}
