using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 冷却、持续状态、相邻状态反应与周期结算（领域战斗层内部）。
internal static class BattleStatusResolver
{
    // 英雄隐身按逻辑Tick自然衰减，到期在本Tick攻击结算前失效。
    internal static void AdvanceHeroStates(BattleRuntime runtime)
    {
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var hero = runtime.GetHero(side);
            if (hero.StealthDurationTicks <= 0) continue;
            hero.StealthDurationTicks--;
            if (hero.StealthDurationTicks == 0)
                runtime.Events.Add(new HeroStealthChangedEvent(runtime.Tick, side, 0));
        }
    }

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
        if (source is CardBattleState { Destroyed: false, IsOnBench: false } recipient)
            foreach (var aura in recipient.Abilities.Where(a => a.Definition.Activation == AbilityActivation.PassiveAura))
                foreach (var multiplier in aura.Definition.Effects.OfType<MultiplyReceivedCardStatusDurationEffectDefinition>())
                    if (multiplier.Status == effect.Status)
                        effect = effect with { Amount = checked(effect.Amount * multiplier.Multiplier) };
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

    // 疾速加成属于施加来源，累加一次后再交给目标倍率；其他状态不受影响。
    internal static int DurationWithSourceBonus(BattleRuntime runtime, PendingAbility pending, BattleStatus status, int amount)
        => status == BattleStatus.HasteDuration
            ? checked(amount + BattleEffectResolver.GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.HasteDurationBonus))
            : amount;

    // 对直接相邻卡牌施加状态，再按稳定顺序处理状态获得反应。
    internal static void ApplyStatusToAdjacentAlliedCards(
        BattleRuntime runtime,
        PendingAbility pending,
        CardBattleState source,
        ApplyStatusToAdjacentAlliedCardsEffectDefinition effect)
    {
        var sourceEnd = source.BoardStart + source.OccupiedSlots;
        foreach (var card in runtime.Cards)
        {
            if (card.Side != source.Side || card.EntityId == source.EntityId || card.Destroyed || card.IsOnBench)
                continue;
            if (effect.RequiredTag is { IsEmpty: false } && !card.Tags.Contains(effect.RequiredTag))
                continue;
            var cardEnd = card.BoardStart + card.OccupiedSlots;
            if (cardEnd != source.BoardStart && card.BoardStart != sourceEnd)
                continue;
            var baseAmount = DurationWithSourceBonus(runtime, pending, effect.Status, effect.Amount);
            var amount = card.Tags.Contains(effect.BonusTag)
                ? checked(baseAmount * effect.BonusMultiplier)
                : baseAmount;
            ApplyCardStatus(runtime, pending, card, effect.Status, amount);
        }
    }

    // 只从存活敌方战场卡牌中按冻结顺序随机选择，复用飞行减时长与状态反应。
    internal static void ApplyStatusToRandomEnemyCard(BattleRuntime runtime, PendingAbility pending,
        ApplyStatusToRandomEnemyCardEffectDefinition effect)
    {
        var candidates = runtime.Cards.Where(card => card.Side != pending.Source.Side && !card.Destroyed && !card.IsOnBench).ToArray();
        if (candidates.Length == 0) return;
        for (var selected = 0; selected < Math.Min(effect.TargetCount, candidates.Length); selected++)
        {
            var index = selected + runtime.NextRandomIndex(candidates.Length - selected);
            var target = candidates[index];
            (candidates[selected], candidates[index]) = (candidates[index], candidates[selected]);
            ApplyCardStatus(runtime, pending, target, effect.Status,
                DurationWithSourceBonus(runtime, pending, effect.Status, effect.Amount));
        }
    }

    // 按冻结顺序随机选择一张存活己方战场卡牌，包含来源自身。
    internal static void ApplyStatusToRandomAlliedCard(BattleRuntime runtime, PendingAbility pending,
        ApplyStatusToRandomAlliedCardEffectDefinition effect)
    {
        var candidates = runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
            && (effect.RequiredTag is not { } tag || BattleEffectResolver.HasEffectiveTag(runtime, card, tag))).ToArray();
        if (candidates.Length == 0) return;
        ApplyCardStatus(runtime, pending, candidates[runtime.NextRandomIndex(candidates.Length)], effect.Status,
            DurationWithSourceBonus(runtime, pending, effect.Status, effect.Amount));
    }

    private static void ApplyCardStatus(BattleRuntime runtime, PendingAbility pending, CardBattleState target,
        BattleStatus status, int amount)
    {
        var applied = ApplyStatus(target, runtime.GetHero(target.Side), new ApplyStatusEffectDefinition(status, amount));
        runtime.Events.Add(new StatusChangedEvent(runtime.Tick, status, applied));
        if (applied > 0) ApplyAdjacentStatusReactions(runtime, target, status);
        if (applied > 0 && !pending.IsEcho)
            CombatSimulator.EnqueueSourceStatusGainedEchoes(runtime, target, status);
        if (applied > 0 && status == BattleStatus.SlowDuration && !pending.IsEcho)
            CombatSimulator.EnqueueSlowAppliedEchoes(runtime, pending.Source.Side);
    }

    // 光环变动只加减自身贡献，同时调整当前剩余冷却，保留已走过的进度。
    internal static void RefreshCooldownAuras(BattleRuntime runtime)
    {
        foreach (var source in runtime.Cards)
        {
            var bonus = 0;
            if (!source.Destroyed && !source.IsOnBench)
                foreach (var ability in source.Abilities)
                foreach (var effect in ability.Definition.Effects)
                {
                    if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                    if (effect is IncreaseSourceCooldownPerBattlefieldElementCardEffectDefinition aura)
                        bonus = checked(bonus + runtime.Cards.Count(card => !card.Destroyed && !card.IsOnBench
                            && card.ElementKeys.Contains(aura.ElementKey)) * aura.AmountTicks);
                    if (effect is ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition taggedAura)
                        bonus = checked(bonus - runtime.Cards.Count(card => card.Side == source.Side
                            && !card.Destroyed && !card.IsOnBench
                            && BattleEffectResolver.HasEffectiveTag(runtime, card, taggedAura.RequiredTag)) * taggedAura.AmountTicks);
                    if (effect is IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition adjacentIncrease)
                        bonus = checked(bonus + runtime.Cards.Count(card => card.EntityId != source.EntityId
                            && card.Side == source.Side && !card.Destroyed && !card.IsOnBench
                            && BattleEffectResolver.HasEffectiveTag(runtime, card, adjacentIncrease.RequiredTag)
                            && (card.BoardStart + card.OccupiedSlots == source.BoardStart
                                || source.BoardStart + source.OccupiedSlots == card.BoardStart)) * adjacentIncrease.AmountTicks);
                    if (effect is ReduceSourceCooldownWhileInStateEffectDefinition stateAura
                        && (stateAura.StateKey == GameAttributeKeys.Flying ? source.IsFlying : source.IsBerserk))
                        bonus = checked(bonus - stateAura.AmountTicks);
                    if (effect is ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition adjacentAura
                        && runtime.Cards.Any(card => card.EntityId != source.EntityId && card.Side == source.Side
                            && !card.Destroyed && !card.IsOnBench
                            && BattleEffectResolver.HasEffectiveTag(runtime, card, adjacentAura.RequiredTag)
                            && (card.BoardStart + card.OccupiedSlots == source.BoardStart
                                || source.BoardStart + source.OccupiedSlots == card.BoardStart)))
                        bonus = checked(bonus - adjacentAura.AmountTicks);
                }
            var reduction = source.Destroyed || source.IsOnBench ? 0 : Math.Min(100, runtime.AbilitySources
                .Where(provider => !provider.Destroyed && !provider.IsOnBench && provider.Side == source.Side)
                .SelectMany(provider => provider.Abilities)
                .Where(ability => ability.Definition.Activation == AbilityActivation.PassiveAura)
                .SelectMany(ability => ability.Definition.Effects)
                .Sum(effect => effect switch
                {
                    ReduceAlliedElementCardCooldownAuraEffectDefinition aura when source.ElementKeys.Contains(aura.ElementKey) => aura.Percent,
                    ReduceOtherHeroCardCooldownAuraEffectDefinition aura when source.Identity is { } identity
                        && runtime.GetHero(source.Side).Identity is { } heroIdentity
                        && identity.FactionKey != heroIdentity.FactionKey => aura.Percent,
                    _ => 0,
                }));
            var delta = bonus - source.CooldownAuraBonusTicks;
            if (delta == 0 && reduction == source.CooldownAuraReductionPercent) continue;
            var oldMultiplier = source.EffectiveCooldownMultiplier;
            var oldBonus = source.CooldownBonusTicks;
            source.CooldownBonusTicks = checked(source.CooldownBonusTicks + delta);
            source.CooldownAuraBonusTicks = bonus;
            source.CooldownAuraReductionPercent = reduction;
            foreach (var ability in source.Abilities)
                if (ability.Definition.Activation == AbilityActivation.Active)
                    ability.RemainingCooldownUnits = Math.Max(0, ability.RemainingCooldownUnits
                        + BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, source.CooldownBonusTicks, source.EffectiveCooldownMultiplier)
                        - BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, oldBonus, oldMultiplier));
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
        if (runtime.Tick.Value % 6 == 0)
        {
            if (hero.Burn > 0) BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, side, hero, hero.Burn, false, DamageSourceKind.Status, BattleStatus.Burn);
            hero.Burn = Math.Max(0, hero.Burn - 1);
        }
        if (runtime.Tick.Value % 10 == 0)
        {
            if (hero.Poison > 0) BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, side, hero, hero.Poison, true, DamageSourceKind.Status, BattleStatus.Poison);
            hero.Health = Math.Min(hero.MaxHealth, checked(hero.Health + hero.HealthRegen));
            hero.Mana = Math.Min(hero.MaxMana, checked(hero.Mana + hero.ManaRegen));
        }
    }

    // 达到日蚀时间时对双方按原曲线结算伤害，优先扣护甲。
    internal static void ApplyEclipse(BattleRuntime runtime, BattleSetup setup)
    {
        if (!runtime.EclipseEntered && runtime.Tick.Value >= setup.EclipseTime.Value)
            CombatSimulator.EnterEclipse(runtime);
        if (!runtime.EclipseEntered || runtime.Tick.Value % 10 != 0) return;
        var seconds = (runtime.Tick.Value - runtime.EclipseTime.Value) / 10;
        var damage = 1 << (int)Math.Min(seconds, 30);
        BattleEffectResolver.ApplyDamage(runtime, runtime.PlayerHero.EntityId, SideId.Player, runtime.PlayerHero,
            ReceivedDamage(SideId.Player), false, DamageSourceKind.Eclipse);
        BattleEffectResolver.ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Opponent, runtime.OpponentHero,
            ReceivedDamage(SideId.Opponent), false, DamageSourceKind.Eclipse);

        int ReceivedDamage(SideId target)
        {
            var amount = damage;
            foreach (var source in runtime.AbilitySources)
            foreach (var ability in source.Abilities)
            {
                if (source.Destroyed || source.IsOnBench || ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                var side = ability.Definition.Target == AbilityTarget.EnemyHero
                    ? (source.Side == SideId.Player ? SideId.Opponent : SideId.Player) : source.Side;
                if (side != target) continue;
                foreach (var effect in ability.Definition.Effects.OfType<MultiplyReceivedEclipseDamageEffectDefinition>())
                    amount = checked(amount * effect.Multiplier);
            }
            return amount;
        }
    }


}
