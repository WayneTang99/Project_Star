namespace Project_Star.Core.States;

// 卡牌战斗内临时状态（战斗层）：冷却/超频/麻痹/禁锢/是否被摧毁等，战斗结束丢弃。
public class CardBattleState : BattleState
{
    public int Bonus { get => GetValue(nameof(Bonus)); set => SetValue(nameof(Bonus), value); }
    public int Cooldown { get => GetValue(nameof(Cooldown)); set => SetValue(nameof(Cooldown), value); }
    public int OverclockDuration { get => GetValue(nameof(OverclockDuration)); set => SetValue(nameof(OverclockDuration), value); }
    public int ParalysisDuration { get => GetValue(nameof(ParalysisDuration)); set => SetValue(nameof(ParalysisDuration), value); }
    public int ImmobilizeDuration { get => GetValue(nameof(ImmobilizeDuration)); set => SetValue(nameof(ImmobilizeDuration), value); }
    public bool Destroyed { get; set; }
}