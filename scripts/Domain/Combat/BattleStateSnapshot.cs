using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 战斗层冻结的英雄数值，不从结算后的对局推算临时状态。
public sealed record HeroBattleSnapshot(int Health, int MaxHealth, int Armor, int Mana, int MaxMana,
    int Burn, int Poison, int HealthRegen, int ManaRegen);

// 单张卡牌的战斗状态；冷却使用十进制半 Tick 单位，保留百分比缩减与疾速/迟缓精度。
public sealed record CardBattleSnapshot(EntityId Id, SideId Side, bool Destroyed,
    int Haste, int Slow, int Immobilize, IReadOnlyList<decimal> CooldownUnits,
    IReadOnlyDictionary<StringName, int> Values)
{
    public CardIdentityAttributes? TransformedIdentity { get; init; }
    public IReadOnlyList<StringName> TransformedTags { get; init; } = System.Array.Empty<StringName>();
    public int Level { get; init; } = 1;
    public IReadOnlyList<AbilityDefinition> TransformedAbilities { get; init; } = System.Array.Empty<AbilityDefinition>();
    public IReadOnlyList<decimal> CooldownDurationUnits { get; init; } = System.Array.Empty<decimal>();
    public bool IsFlying { get; init; }
    public bool IsOnBench { get; init; }
    public bool IsBerserk { get; init; }
}

// EventCount 定位已消费的原日志前缀，同 Tick 的快照保持生成顺序。
public sealed record BattleStateSnapshot(BattleTick Tick, int EventCount, bool Eclipse,
    HeroBattleSnapshot Player, HeroBattleSnapshot Opponent, IReadOnlyList<CardBattleSnapshot> Cards);
