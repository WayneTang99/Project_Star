using Godot;

namespace Project_Star.Domain.Common;

public static class GameAttributeKeys
{
    public static readonly StringName Level = new("Level");
    public static readonly StringName Value = new("Value");
    public static readonly StringName Wealth = new("Wealth");
    public static readonly StringName MaxHealth = new("MaxHealth");
    public static readonly StringName Armor = new("Armor");
    public static readonly StringName AttackDamage = new("AttackDamage");
    public static readonly StringName CooldownTicks = new("CooldownTicks");
    public static readonly StringName MaxMana = new("MaxMana");
    public static readonly StringName Mana = new("Mana");
    public static readonly StringName ManaRegen = new("ManaRegen");
    public static readonly StringName HealthRegen = new("HealthRegen");
    public static readonly StringName Burn = new("Burn");
    public static readonly StringName Poison = new("Poison");
    public static readonly StringName Reputation = new("Reputation");
}
