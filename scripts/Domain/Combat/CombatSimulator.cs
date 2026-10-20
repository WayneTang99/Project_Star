using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 确定性战斗入口：固定 Tick 顺序、FIFO 发动与终止判定（领域战斗层）。
public sealed class CombatSimulator
{
    // 备战只读预览：仅计算当前光环，不发动能力、不推进随机或战斗进度。
    public BattleStateSnapshot Preview(BattleSetup setup, bool hasOpponent = true)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var runtime = new BattleRuntime(setup);
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        BattleStateRecorder.CaptureState(runtime, hasOpponent);
        return runtime.States[0];
    }

    // 只计算一次战斗，结果包含原始日志和对应时间点的冻结状态。
    public BattleResult Simulate(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var runtime = new BattleRuntime(setup);
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        BattleStateRecorder.CaptureState(runtime);
        runtime.Events.Add(new BattleStartedEvent(runtime.Tick));
        EnqueueBattleStartAbilities(runtime);
        ResolveQueue(runtime);
        var openingResult = TryCreateDefeatResult(runtime);
        if (openingResult is not null) return openingResult;
        while (runtime.Tick.Value < setup.Timeout.Value)
        {
            runtime.Tick += new BattleTick(1);
            BattleStatusResolver.AdvanceHeroStates(runtime);
            BattleStatusResolver.ApplyEclipse(runtime, setup);
            BattleStatusResolver.AdvanceCooldowns(runtime);
            EnqueueScheduledMulticasts(runtime);
            EnqueueReadyActives(runtime);
            BattleStatusResolver.SettleHeroStatuses(runtime);
            BattleStateRecorder.CaptureState(runtime);
            ResolveQueue(runtime);
            var result = TryCreateDefeatResult(runtime);
            if (result is not null) return result;
        }

        runtime.PlayerHero.Health = 0;
        runtime.OpponentHero.Health = 0;
        return BattleStateRecorder.CreateResult(runtime, BattleOutcome.PlayerVictory, BattleEndReason.Extinction);
    }

    private static void EnqueueBattleStartAbilities(BattleRuntime runtime)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.PassiveOnBattleStart)
                Enqueue(runtime, source, ability, false);
        }
    }

    // 日蚀状态先冻结，再按稳定来源顺序即时执行一次进入效果，早于日蚀伤害。
    internal static void EnterEclipse(BattleRuntime runtime)
    {
        if (runtime.EclipseEntered) return;
        runtime.EclipseEntered = true;
        if (runtime.Tick.Value < runtime.EclipseTime.Value) runtime.EclipseTime = runtime.Tick;
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || source.IsOnBench || ability.Triggered
                || ability.Definition.Activation != AbilityActivation.PassiveOnEclipseStart) continue;
            ability.Triggered = true;
            var pending = new PendingAbility(source, ability, true, false, runtime.Tick);
            runtime.Events.Add(new AbilityActivatedEvent(runtime.Tick, source.EntityId, source.Side, true,
                BattleEffectResolver.GetAbilitySourceKind(source)));
            foreach (var effect in ability.Definition.Effects)
            {
                BattleEffectResolver.ApplyEffect(runtime, pending, effect);
                BattleStatusResolver.RefreshCooldownAuras(runtime);
                BattleStateRecorder.CaptureState(runtime);
            }
        }
    }

    private static void EnqueueReadyActives(BattleRuntime runtime)
    {
        foreach (var card in runtime.Cards)
        foreach (var ability in card.Abilities)
        {
            if (!card.Destroyed && !card.IsOnBench && ability.Definition.Activation == AbilityActivation.Active
                && ability.RemainingCooldownUnits == 0)
                Enqueue(runtime, card, ability, false);
        }
    }

    // 延迟多重按到期时间及首次排程顺序入队；每次发动独立触发回响。
    private static void EnqueueScheduledMulticasts(BattleRuntime runtime)
    {
        var due = runtime.ScheduledMulticasts.Where(item => item.Due.Value <= runtime.Tick.Value)
            .OrderBy(item => item.Due.Value).ToArray();
        runtime.ScheduledMulticasts.RemoveAll(item => item.Due.Value <= runtime.Tick.Value);
        foreach (var item in due)
            if ((!item.Ability.Source.Destroyed || item.Ability.AllowDestroyedSource) && !item.Ability.Source.IsOnBench
                && item.Ability.Source is CardBattleState card && runtime.Cards.Contains(card))
                Enqueue(runtime, card, item.Ability.Ability, false, true, suppressEchoes: item.Ability.SuppressEchoes, triggeredActivation: item.Ability.IsTriggeredActivation,
                    allowDestroyedSource: item.Ability.AllowDestroyedSource, triggerChain: item.Ability.TriggerChain);
    }

    // 将发动追加到 FIFO，并按原顺序记录排队事件。
    internal static void Enqueue(
        BattleRuntime runtime,
        IBattleAbilitySource card,
        BattleAbilityState ability,
        bool echo,
        bool multicast = false,
        CardBattleState? eventCard = null,
        bool suppressEchoes = false,
        bool triggeredActivation = false,
        bool allowDestroyedSource = false,
        HashSet<EntityId>? triggerChain = null)
    {
        runtime.Queue.Enqueue(new PendingAbility(card, ability, echo, multicast, runtime.Tick, eventCard) { SuppressEchoes = suppressEchoes, IsTriggeredActivation = triggeredActivation,
            AllowDestroyedSource = allowDestroyedSource, TriggerChain = triggerChain });
        runtime.Events.Add(new AbilityQueuedEvent(
            runtime.Tick,
            card.EntityId,
            card.Side,
            BattleEffectResolver.GetAbilitySourceKind(card)));
    }

    private static void ResolveQueue(BattleRuntime runtime)
    {
        while (runtime.Queue.Count > 0)
        {
            var pending = runtime.Queue.Dequeue();
            if (pending.Source.Destroyed && !pending.AllowDestroyedSource) continue;
            if (pending.Source is CardBattleState queuedCard && !runtime.Cards.Contains(queuedCard)) continue;
            if (pending.Ability.Definition.Target == AbilityTarget.EventCard
                && (pending.EventCard is null || pending.EventCard.Destroyed || pending.EventCard.IsOnBench
                    || !runtime.Cards.Contains(pending.EventCard))) continue;
            var hero = runtime.GetHero(pending.Source.Side);
            var definition = pending.Ability.Definition;
            var paysMana = definition.Activation == AbilityActivation.Active && !pending.IsMulticast && !pending.IsTriggeredActivation;
            if (paysMana && hero.Mana < definition.ManaCost) continue;
            var activatesLeftmostCard = pending.Source is CardBattleState { IsOnBench: false } activatingCard
                && !runtime.Cards.Any(card => card.Side == activatingCard.Side && !card.Destroyed && !card.IsOnBench
                    && card.BoardStart < activatingCard.BoardStart);
            var activatesShortestCooldownCard = definition.Activation == AbilityActivation.Active
                && !pending.IsEcho && !pending.SuppressEchoes
                && ReferenceEquals(pending.Source, runtime.Cards
                    .Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                        && card.Abilities.Any(active => active.Definition.Activation == AbilityActivation.Active))
                    .OrderBy(card => card.Abilities.Where(active => active.Definition.Activation == AbilityActivation.Active)
                        .Max(active => BattleCooldownRules.DurationUnits(active.Definition.CooldownTicks,
                            card.CooldownBonusTicks, card.EffectiveCooldownMultiplier)))
                    .ThenBy(card => card.BoardStart).FirstOrDefault());
            if (paysMana)
            {
                hero.Mana -= definition.ManaCost;
                hero.ManaSpent = checked(hero.ManaSpent + definition.ManaCost);
            }
            if (definition.ManaCost > 0 && paysMana)
                runtime.Events.Add(new ManaChangedEvent(runtime.Tick, pending.Source.Side, -definition.ManaCost, hero.Mana));
            if (!pending.IsEcho && !pending.IsMulticast && !pending.IsTriggeredActivation)
                pending.Ability.RemainingCooldownUnits = BattleCooldownRules.DurationUnits(definition.CooldownTicks,
                    pending.Source.CooldownBonusTicks,
                    pending.Source is CardBattleState cooldownCard ? cooldownCard.EffectiveCooldownMultiplier : 1m);
            runtime.Events.Add(new AbilityActivatedEvent(
                runtime.Tick,
                pending.Source.EntityId,
                pending.Source.Side,
                pending.IsEcho,
                BattleEffectResolver.GetAbilitySourceKind(pending.Source)));
            BattleStateRecorder.CaptureState(runtime);
            if (pending.TriggerChain is null && definition.Effects.Any(effect => effect is TriggerOtherAlliedElementActivesEffectDefinition or ActivateSourceCardEffectDefinition))
                pending = pending with { TriggerChain = new HashSet<EntityId> { pending.Source.EntityId } };
            if (!pending.IsEcho && !pending.IsMulticast && pending.Source is CardBattleState cardSource)
            {
                var multicast = BattleEffectResolver.GetEffectiveMulticast(runtime, cardSource);
                for (var repeat = 0; repeat < multicast; repeat++)
                    runtime.ScheduledMulticasts.Add((runtime.Tick + new BattleTick(checked((repeat + 1) * 2)),
                        pending with { IsMulticast = true, EnqueuedAt = runtime.Tick }));
            }
            pending = ElementSequenceResolver.Consume(runtime, pending);
            runtime.ResolvingActivation = pending;
            if (definition.Effects.Any(effect => effect is ApplyPrimaryEffectPercentStatusEffectDefinition))
                pending = pending with { PrimaryValue = BattleEffectResolver.PrimaryValue(runtime, pending) };
            foreach (var effect in definition.Effects)
            {
                BattleEffectResolver.ApplyEffect(runtime, pending, effect);
                BattleStatusResolver.RefreshCooldownAuras(runtime);
                BattleStateRecorder.CaptureState(runtime);
            }
            runtime.ResolvingActivation = null;
            ElementSequenceResolver.Complete(runtime, pending);
            if (!pending.IsEcho) pending.Source.ActivationCount++;
            if (!pending.IsEcho && definition.Activation == AbilityActivation.Active
                && pending.Source is CardBattleState activatedCard)
            {
                activatedCard.ActiveActivationCount++;
                if (activatedCard.ActiveActivationCount == 1 && activatedCard.Abilities.Any(ability =>
                    ability.Definition.Effects.Any(effect => effect is AttributeDamageEffectDefinition { FirstActivationMultiplier: > 1 })))
                    BattleStateRecorder.CaptureState(runtime);
            }
            if (!pending.IsEcho && !pending.SuppressEchoes)
                EnqueueEchoes(runtime, pending, activatesLeftmostCard, activatesShortestCooldownCard);
            if (!pending.IsEcho && definition.Activation == AbilityActivation.Active
                && pending.Source is CardBattleState questCard && questCard.Quests.Count > 0
                && runtime.Cards.Contains(questCard))
                BattleQuestResolver.AdvanceSourceQuests(runtime, questCard);
        }
    }

    private static void EnqueueEchoes(BattleRuntime runtime, PendingAbility origin, bool activatesLeftmostCard,
        bool activatesShortestCooldownCard)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAbilityActivated)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnAlliedManaSpent
                && source.Side == origin.Source.Side
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && origin.Ability.Definition.ManaCost > 0
                && !origin.IsMulticast && !origin.IsTriggeredActivation)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnEnemyCardActivated
                && source.Side != origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } enemyCard
                && (ability.Definition.TriggerCardAnyTags is not { } enemyTags
                    || enemyTags.Any(tag => BattleEffectResolver.HasEffectiveTag(runtime, enemyCard, tag)))
                && origin.Ability.Definition.Activation == AbilityActivation.Active)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnShortestCooldownEnemyCardActivated
                && source.Side != origin.Source.Side
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && activatesShortestCooldownCard)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnAnyAttackCardActivated
                && origin.Source is CardBattleState { IsOnBench: false } eventCard
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && eventCard.IsAttackCard)
                Enqueue(runtime, source, ability, true, eventCard: eventCard);
            if (ability.Definition.Activation == AbilityActivation.EchoOnSourceCardActivated
                && ReferenceEquals(source, origin.Source) && source is CardBattleState
                && origin.Ability.Definition.Activation == AbilityActivation.Active)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnLeftmostAlliedCardActivated
                && source.Side == origin.Source.Side
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && activatesLeftmostCard)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnAdjacentAlliedAttackCardActivated
                && source is CardBattleState adjacentSource && source.Side == origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } attackingCard
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && (adjacentSource.BoardStart + adjacentSource.OccupiedSlots == attackingCard.BoardStart
                    || attackingCard.BoardStart + attackingCard.OccupiedSlots == adjacentSource.BoardStart)
                && (ability.Definition.TriggerCardTag is not { } requiredTag || BattleEffectResolver.HasEffectiveTag(runtime, attackingCard, requiredTag))
                && attackingCard.IsAttackCard)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnAdjacentAlliedCardActivated
                && source is CardBattleState directionalSource && source.Side == origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } activatedCard
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && (ability.Definition.TriggerCardTag is not { } adjacentTag || BattleEffectResolver.HasEffectiveTag(runtime, activatedCard, adjacentTag))
                && (ability.Definition.TriggerCardSize is not { } adjacentSize || activatedCard.OccupiedSlots == (int)adjacentSize)
                && (ability.Definition.TriggerCardSide == AdjacentCardSide.Left
                    ? activatedCard.BoardStart + activatedCard.OccupiedSlots == directionalSource.BoardStart
                    : activatedCard.BoardStart == directionalSource.BoardStart + directionalSource.OccupiedSlots))
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated
                && source.Side == origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } originCard
                && (!ability.Definition.ExcludeSourceCard || originCard.EntityId != source.EntityId)
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && (ability.Definition.TriggerCardSize is not { } size || originCard.OccupiedSlots == (int)size)
                && (ability.Definition.TriggerCardTag is null && ability.Definition.TriggerCardElement is null
                    || ability.Definition.TriggerCardTag is { } tag && BattleEffectResolver.HasEffectiveTag(runtime, originCard, tag)
                    || ability.Definition.TriggerCardElement is { } element && originCard.ElementKeys.Contains(element)))
                Enqueue(runtime, source, ability, true, eventCard: originCard);
            if (ability.Definition.Activation == AbilityActivation.EchoOnFirstAlliedCardActivated
                && !ability.Triggered
                && source.Side == origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } firstCard
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && (ability.Definition.TriggerCardTag is not { } firstTag || BattleEffectResolver.HasEffectiveTag(runtime, firstCard, firstTag))
                && !origin.IsMulticast)
            {
                ability.Triggered = true;
                Enqueue(runtime, source, ability, true, eventCard: firstCard);
            }
        }
    }

    // 首次致命伤害或越过生命阈值后立即结算一次性回响，早于死亡判定和后续攻击。
    internal static void ResolveHeroHealthTriggers(BattleRuntime runtime, SideId side, int previousHealth)
    {
        var hero = runtime.GetHero(side);
        var firstLethal = previousHealth > 0 && hero.Health == 0 && !hero.FirstLethalDamageReceived;
        if (hero.Health <= 0 && !firstLethal) return;
        if (firstLethal) hero.FirstLethalDamageReceived = true;
        var triggers = runtime.AbilitySources.Where(source => source.Side == side && !source.Destroyed && !source.IsOnBench)
            .SelectMany(source => source.Abilities.Select(ability => (Source: source, Ability: ability)))
            .Where(item => !item.Ability.Triggered && (firstLethal
                ? item.Ability.Definition.Activation == AbilityActivation.EchoOnSourceHeroFirstLethalDamage
                : item.Ability.Definition.Activation == AbilityActivation.EchoOnSourceHeroHealthBelow
                    && (long)previousHealth * 100 >= (long)hero.MaxHealth * item.Ability.Definition.TriggerHeroHealthPercent!.Value
                    && (long)hero.Health * 100 < (long)hero.MaxHealth * item.Ability.Definition.TriggerHeroHealthPercent!.Value)).ToArray();
        foreach (var item in triggers) item.Ability.Triggered = true;
        foreach (var item in triggers)
        {
            if (item.Source.Destroyed) continue;
            var pending = new PendingAbility(item.Source, item.Ability, true, false, runtime.Tick);
            pending = pending with { PrimaryValue = BattleEffectResolver.PrimaryValue(runtime, pending) };
            runtime.Events.Add(new AbilityActivatedEvent(runtime.Tick, item.Source.EntityId, side, true,
                BattleEffectResolver.GetAbilitySourceKind(item.Source)));
            BattleStateRecorder.CaptureState(runtime);
            foreach (var effect in item.Ability.Definition.Effects)
            {
                BattleEffectResolver.ApplyEffect(runtime, pending, effect);
                BattleStatusResolver.RefreshCooldownAuras(runtime);
                BattleStateRecorder.CaptureState(runtime);
            }
        }
    }

    // 在非回响伤害发生后按稳定能力来源顺序排入回响。
    internal static void EnqueueDamageEchoes(BattleRuntime runtime)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnDamageDealt)
                Enqueue(runtime, source, ability, true);
        }
    }

    // 己方真实施加迟缓后排入回响；按施加方筛选，不按受影响卡牌的归属筛选。
    internal static void EnqueueSlowAppliedEchoes(BattleRuntime runtime, SideId applyingSide)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Side != applyingSide || source.Destroyed || source.IsOnBench) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAlliedSlowApplied)
                Enqueue(runtime, source, ability, true);
        }
    }

    // 正时长状态实际累加后，按受影响卡牌归属排入自身、己方或敌方状态获得回响。
    internal static void EnqueueSourceStatusGainedEchoes(BattleRuntime runtime, CardBattleState target, BattleStatus status)
    {
        if (target.Destroyed || target.IsOnBench) return;
        foreach (var ability in target.Abilities)
            if (ability.Definition.Activation == AbilityActivation.EchoOnSourceCardStatusGained
                && ability.Definition.TriggerStatus == status)
                Enqueue(runtime, target, ability, true);
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || source.IsOnBench) continue;
            if ((ability.Definition.Activation == AbilityActivation.EchoOnAlliedCardStatusGained && source.Side == target.Side
                    || ability.Definition.Activation == AbilityActivation.EchoOnEnemyCardStatusGained && source.Side != target.Side)
                && ability.Definition.TriggerStatus == status)
                Enqueue(runtime, source, ability, true, eventCard: target);
        }
    }

    // 真实进入布尔状态后，按稳定来源顺序排入己方回响并保存事件目标。
    internal static void EnqueueStateEntryEchoes(BattleRuntime runtime, CardBattleState target, StringName stateKey)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Side != target.Side || source.Destroyed || source.IsOnBench) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAlliedCardEnteredState
                && ability.Definition.TriggerStateKey == stateKey)
                Enqueue(runtime, source, ability, true, eventCard: target);
        }
    }

    private static BattleResult? TryCreateDefeatResult(BattleRuntime runtime)
    {
        var player = runtime.PlayerHero.Health <= 0; var opponent = runtime.OpponentHero.Health <= 0;
        if (!player && !opponent) return null;
        if (player) runtime.Events.Add(new HeroDefeatedEvent(runtime.Tick, SideId.Player));
        if (opponent) runtime.Events.Add(new HeroDefeatedEvent(runtime.Tick, SideId.Opponent));
        if (player && opponent) return BattleStateRecorder.CreateResult(runtime, BattleOutcome.PlayerVictory, BattleEndReason.SimultaneousDefeat);
        return BattleStateRecorder.CreateResult(runtime, opponent ? BattleOutcome.PlayerVictory : BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated);
    }


}
