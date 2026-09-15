namespace Project_Star.Core.States;

public class HeroBattleState : BattleState
{
    public int Health { get => GetValue(nameof(Health)); set => SetValue(nameof(Health), value); }
    public int MaxHealth { get => GetValue(nameof(MaxHealth)); set => SetValue(nameof(MaxHealth), value); }
    public int Armor { get => GetValue(nameof(Armor)); set => SetValue(nameof(Armor), value); }
    public int Radiation { get => GetValue(nameof(Radiation)); set => SetValue(nameof(Radiation), value); }
    public int Corrosion { get => GetValue(nameof(Corrosion)); set => SetValue(nameof(Corrosion), value); }
    public int HealthRegen { get => GetValue(nameof(HealthRegen)); set => SetValue(nameof(HealthRegen), value); }
    public int Energy { get => GetValue(nameof(Energy)); set => SetValue(nameof(Energy), value); }
    public int MaxEnergy { get => GetValue(nameof(MaxEnergy)); set => SetValue(nameof(MaxEnergy), value); }
    public int EnergyRegen { get => GetValue(nameof(EnergyRegen)); set => SetValue(nameof(EnergyRegen), value); }
}