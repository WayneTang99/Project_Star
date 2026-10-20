using System.Collections.Generic;
using System.Linq;

namespace Project_Star.Domain.Combat;

// 元素发动联动窗口的查询、一次性消费与刷新；仅修改本场能力状态（领域战斗层）。
internal static class ElementSequenceResolver
{
    internal static PendingAbility Consume(BattleRuntime runtime, PendingAbility pending)
    {
        if (!Eligible(pending)) return pending;
        var card = (CardBattleState)pending.Source;
        var multiplier = 1m;
        var followups = new List<(IBattleAbilitySource, BattleAbilityState)>();
        foreach (var (source, ability, rule) in Rules(runtime, card))
        {
            if (!Matches(runtime, card, pending.Ability, ability, rule)) continue;
            ability.SequenceExpiresAt = -1;
            multiplier = checked(multiplier * rule.DamageMultiplier);
            if (rule.Status is not null) followups.Add((source, ability));
        }
        return pending with { DamageMultiplier = multiplier, SequenceFollowups = followups.AsReadOnly() };
    }

    internal static void Complete(BattleRuntime runtime, PendingAbility pending)
    {
        if (!Eligible(pending)) return;
        var card = (CardBattleState)pending.Source;
        foreach (var (_, ability, rule) in Rules(runtime, card))
            if (card.ElementKeys.Contains(rule.PrerequisiteElement))
                ability.SequenceExpiresAt = checked(runtime.Tick.Value + rule.WindowTicks);
        foreach (var (source, ability) in pending.SequenceFollowups)
            if (!source.Destroyed && !source.IsOnBench)
                CombatSimulator.Enqueue(runtime, source, ability, true, eventCard: card);
    }

    // 预览不消费窗口；正在发动时使用已冻结的本次倍率。
    internal static decimal PreviewMultiplier(BattleRuntime runtime, CardBattleState card, BattleAbilityState active)
    {
        if (runtime.ResolvingActivation is { } pending && ReferenceEquals(pending.Source, card)
            && ReferenceEquals(pending.Ability, active)) return pending.DamageMultiplier;
        if (active.Definition.Activation != AbilityActivation.Active || card.Destroyed || card.IsOnBench) return 1m;
        var multiplier = 1m;
        foreach (var (_, ability, rule) in Rules(runtime, card))
            if (Matches(runtime, card, active, ability, rule)) multiplier = checked(multiplier * rule.DamageMultiplier);
        return multiplier;
    }

    private static bool Eligible(PendingAbility pending) => !pending.IsEcho && !pending.SuppressEchoes
        && pending.Ability.Definition.Activation == AbilityActivation.Active
        && pending.Source is CardBattleState { IsOnBench: false };
    private static bool Matches(BattleRuntime runtime, CardBattleState card, BattleAbilityState active, BattleAbilityState ability,
        ElementSequenceEffectDefinition rule) => runtime.Tick.Value < ability.SequenceExpiresAt
        && card.ElementKeys.Contains(rule.FollowupElement)
        && (!rule.RequiresAttack || active.Definition.Effects.Any(BattleEffectResolver.IsDamageEffect));
    private static IEnumerable<(IBattleAbilitySource Source, BattleAbilityState Ability, ElementSequenceEffectDefinition Rule)>
        Rules(BattleRuntime runtime, CardBattleState card) => runtime.AbilitySources
            .Where(source => source.Side == card.Side && !source.Destroyed && !source.IsOnBench)
            .SelectMany(source => source.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.PassiveAura)
                .SelectMany(ability => ability.Definition.Effects.OfType<ElementSequenceEffectDefinition>()
                    .Select(rule => (source, ability, rule))));
}
