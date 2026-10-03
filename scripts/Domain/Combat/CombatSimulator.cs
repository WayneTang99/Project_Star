using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 确定性战斗入口：固定 Tick 顺序、FIFO 发动与终止判定（领域战斗层）。
public sealed class CombatSimulator
{
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
            BattleStatusResolver.ApplyEclipse(runtime, setup);
            BattleStatusResolver.AdvanceCooldowns(runtime);
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

    // 将发动追加到 FIFO，并按原顺序记录排队事件。
    internal static void Enqueue(
        BattleRuntime runtime,
        IBattleAbilitySource card,
        BattleAbilityState ability,
        bool echo,
        bool multicast = false,
        CardBattleState? eventCard = null)
    {
        runtime.Queue.Enqueue(new PendingAbility(card, ability, echo, multicast, runtime.Tick, eventCard));
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
            if (pending.Source.Destroyed) continue;
            if (pending.Source is CardBattleState queuedCard && !runtime.Cards.Contains(queuedCard)) continue;
            if (pending.Ability.Definition.Target == AbilityTarget.EventCard
                && (pending.EventCard is null || pending.EventCard.Destroyed || pending.EventCard.IsOnBench
                    || !runtime.Cards.Contains(pending.EventCard))) continue;
            var hero = runtime.GetHero(pending.Source.Side);
            var definition = pending.Ability.Definition;
            var paysMana = definition.Activation == AbilityActivation.Active && !pending.IsMulticast;
            if (paysMana && hero.Mana < definition.ManaCost) continue;
            if (paysMana)
            {
                hero.Mana -= definition.ManaCost;
                hero.ManaSpent = checked(hero.ManaSpent + definition.ManaCost);
            }
            if (definition.ManaCost > 0 && paysMana)
                runtime.Events.Add(new ManaChangedEvent(runtime.Tick, pending.Source.Side, -definition.ManaCost, hero.Mana));
            if (!pending.IsEcho && !pending.IsMulticast)
                pending.Ability.RemainingCooldownUnits = (definition.CooldownTicks + pending.Source.CooldownBonusTicks) * 2
                    * (pending.Source is CardBattleState cooldownCard ? cooldownCard.CooldownMultiplier : 1m);
            runtime.Events.Add(new AbilityActivatedEvent(
                runtime.Tick,
                pending.Source.EntityId,
                pending.Source.Side,
                pending.IsEcho,
                BattleEffectResolver.GetAbilitySourceKind(pending.Source)));
            BattleStateRecorder.CaptureState(runtime);
            if (!pending.IsEcho && !pending.IsMulticast && pending.Source is CardBattleState cardSource)
                for (var repeat = 0; repeat < BattleEffectResolver.GetEffectiveMulticast(runtime, cardSource); repeat++)
                    Enqueue(runtime, cardSource, pending.Ability, false, true);
            foreach (var effect in definition.Effects)
            {
                BattleEffectResolver.ApplyEffect(runtime, pending, effect);
                BattleStatusResolver.RefreshCooldownAuras(runtime);
                BattleStateRecorder.CaptureState(runtime);
            }
            if (!pending.IsEcho) pending.Source.ActivationCount++;
            if (!pending.IsEcho) EnqueueEchoes(runtime, pending);
        }
    }

    private static void EnqueueEchoes(BattleRuntime runtime, PendingAbility origin)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAbilityActivated)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnSourceCardActivated
                && ReferenceEquals(source, origin.Source) && source is CardBattleState
                && origin.Ability.Definition.Activation == AbilityActivation.Active)
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated
                && source.Side == origin.Source.Side
                && origin.Source is CardBattleState { IsOnBench: false } originCard
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && (ability.Definition.TriggerCardTag is { } tag && originCard.Tags.Contains(tag)
                    || ability.Definition.TriggerCardElement is { } element && originCard.ElementKeys.Contains(element)))
                Enqueue(runtime, source, ability, true);
            if (ability.Definition.Activation == AbilityActivation.EchoOnFirstAlliedCardActivated
                && !ability.Triggered
                && source.Side == origin.Source.Side
                && origin.Source is CardBattleState
                && origin.Ability.Definition.Activation == AbilityActivation.Active
                && !origin.IsMulticast)
            {
                ability.Triggered = true;
                Enqueue(runtime, source, ability, true);
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
