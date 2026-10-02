using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 冷却、持续状态、相邻状态反应与周期结算（领域战斗层内部）。
internal static class BattleStatusResolver
{
    // 每 Tick 递减卡牌冷却和状态时长，先按现行疾速/迟缓倍率计算。
    internal static void AdvanceCooldowns(BattleRuntime runtime)
    {
        foreach (var card in runtime.Cards)
        {
            if (card.Destroyed) continue;
            var speed = card.ImmobilizeDuration > 0 ? 0
                : card.HasteDuration > 0 && card.SlowDuration == 0 ? 4
                : card.SlowDuration > 0 && card.HasteDuration == 0 ? 1 : 2;
            foreach (var ability in card.Abilities)
            {
                if (ability.Definition.Activation == AbilityActivation.Active)
                    ability.RemainingCooldownUnits = Math.Max(0, ability.RemainingCooldownUnits - speed);
            }
            card.HasteDuration = Math.Max(0, card.HasteDuration - 1);
            card.SlowDuration = Math.Max(0, card.SlowDuration - 1);
            card.ImmobilizeDuration = Math.Max(0, card.ImmobilizeDuration - 1);
        }
    }

    // 累加英雄或来源卡牌的对应状态贡献。
    internal static int ApplyStatus(IBattleAbilitySource source, HeroBattleState hero, ApplyStatusEffectDefinition effect)
    {
        var amount = source is CardBattleState { IsFlying: true }
            && effect.Status is BattleStatus.SlowDuration or BattleStatus.ImmobilizeDuration
                ? effect.Amount / 2 : effect.Amount;
        switch (effect.Status)
        {
            case BattleStatus.Burn: hero.Burn = checked(hero.Burn + amount); break;
            case BattleStatus.Poison: hero.Poison = checked(hero.Poison + amount); break;
            case BattleStatus.HealthRegen: hero.HealthRegen = checked(hero.HealthRegen + amount); break;
            case BattleStatus.ManaRegen: hero.ManaRegen = checked(hero.ManaRegen + amount); break;
            case BattleStatus.HasteDuration when source is CardBattleState card:
                card.HasteDuration = checked(card.HasteDuration + amount); break;
            case BattleStatus.SlowDuration when source is CardBattleState card:
                card.SlowDuration = checked(card.SlowDuration + amount); break;
            case BattleStatus.ImmobilizeDuration when source is CardBattleState card:
                card.ImmobilizeDuration = checked(card.ImmobilizeDuration + amount); break;
            case BattleStatus.HasteDuration or BattleStatus.SlowDuration or BattleStatus.ImmobilizeDuration:
                throw new InvalidOperationException("Card status requires a card source.");
        }
        return amount;
    }

    // 对直接相邻卡牌施加状态，再按稳定顺序处理状态获得反应。
    internal static void ApplyStatusToAdjacentAlliedCards(
        BattleRuntime runtime,
        CardBattleState source,
        ApplyStatusToAdjacentAlliedCardsEffectDefinition effect)
    {
        var sourceEnd = source.BoardStart + source.OccupiedSlots;
        foreach (var card in runtime.Cards)
        {
            if (card.Side != source.Side || card.EntityId == source.EntityId || card.Destroyed || card.IsOnBench)
                continue;
            var cardEnd = card.BoardStart + card.OccupiedSlots;
            if (cardEnd != source.BoardStart && card.BoardStart != sourceEnd)
                continue;
            var amount = card.Tags.Contains(effect.BonusTag)
                ? checked(effect.Amount * effect.BonusMultiplier)
                : effect.Amount;
            var applied = ApplyStatus(card, runtime.GetHero(card.Side), new ApplyStatusEffectDefinition(effect.Status, amount));
            runtime.Events.Add(new StatusChangedEvent(runtime.Tick, effect.Status, applied));
            if (applied > 0) ApplyAdjacentStatusReactions(runtime, card, effect.Status);
        }
    }

    // 在真实状态获得后执行相邻光环的通用属性反应。
    internal static void ApplyAdjacentStatusReactions(
        BattleRuntime runtime,
        CardBattleState target,
        BattleStatus status)
    {
        foreach (var source in runtime.Cards)
        {
            if (source.Side != target.Side || source.EntityId == target.EntityId
                || source.Destroyed || source.IsOnBench)
            {
                continue;
            }
            var sourceEnd = source.BoardStart + source.OccupiedSlots;
            var targetEnd = target.BoardStart + target.OccupiedSlots;
            if (targetEnd != source.BoardStart && target.BoardStart != sourceEnd) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura
                    || effect is not ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition reaction
                    || reaction.Status != status
                    || !target.Tags.Contains(reaction.RequiredTag)
                    || !target.SupportsCombatAttribute(reaction.AttributeKey))
                {
                    continue;
                }
                var currentValue = target.AddCombatAttribute(reaction.AttributeKey, reaction.Amount);
                runtime.Events.Add(new CardAttributeChangedEvent(
                    runtime.Tick,
                    target.EntityId,
                    reaction.AttributeKey,
                    reaction.Amount,
                    currentValue));
            }
        }
    }

    // 玩家方先结算周期状态，每方完成后冻结状态。
    internal static void SettleHeroStatuses(BattleRuntime runtime)
    {
        SettleHero(runtime, SideId.Player, runtime.PlayerHero);
        BattleStateRecorder.CaptureState(runtime);
        SettleHero(runtime, SideId.Opponent, runtime.OpponentHero);
        BattleStateRecorder.CaptureState(runtime);
    }

    private static void SettleHero(BattleRuntime runtime, SideId side, HeroBattleState hero)
    {
        if (runtime.Tick.Value % 10 == 0)
        {
            if (hero.Burn > 0) BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, side, hero, hero.Burn, false, DamageSourceKind.Status);
            if (hero.Poison > 0) BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, side, hero, hero.Poison, true, DamageSourceKind.Status);
            hero.Health = Math.Min(hero.MaxHealth, checked(hero.Health + hero.HealthRegen));
            hero.Mana = Math.Min(hero.MaxMana, checked(hero.Mana + hero.ManaRegen));
            hero.Poison /= 2;
        }
        if (runtime.Tick.Value % 2 == 0) hero.Burn = Math.Max(0, hero.Burn - 1);
    }

    // 达到日蚀时间时对双方按原曲线结算无视护甲伤害。
    internal static void ApplyEclipse(BattleRuntime runtime, BattleSetup setup)
    {
        if (runtime.Tick.Value < setup.EclipseTime.Value || runtime.Tick.Value % 10 != 0) return;
        var seconds = (runtime.Tick.Value - setup.EclipseTime.Value) / 10;
        var damage = 1 << (int)Math.Min(seconds, 30);
        BattleEffectResolver.ApplyDamage(runtime, runtime.PlayerHero.EntityId, SideId.Player, runtime.PlayerHero, damage, true, DamageSourceKind.Eclipse);
        BattleEffectResolver.ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Opponent, runtime.OpponentHero, damage, true, DamageSourceKind.Eclipse);
    }


}
