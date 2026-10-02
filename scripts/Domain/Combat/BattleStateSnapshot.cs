using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

// 战斗层冻结的英雄数值，不从结算后的对局推算临时状态。
public sealed record HeroBattleSnapshot(int Health, int MaxHealth, int Armor, int Mana, int MaxMana,
    int Burn, int Poison, int HealthRegen, int ManaRegen);

// 单张卡牌的战斗状态；冷却使用半 Tick 单位，避免疾速/迟缓丢失精度。
public sealed record CardBattleSnapshot(EntityId Id, SideId Side, bool Destroyed,
    int Haste, int Slow, int Immobilize, IReadOnlyList<int> CooldownUnits,
    IReadOnlyDictionary<StringName, int> Values)
{
    public bool IsFlying { get; init; }
    public bool IsBerserk { get; init; }
}

// EventCount 定位已消费的原日志前缀，同 Tick 的快照保持生成顺序。
public sealed record BattleStateSnapshot(BattleTick Tick, int EventCount, bool Eclipse,
    HeroBattleSnapshot Player, HeroBattleSnapshot Opponent, IReadOnlyList<CardBattleSnapshot> Cards);
