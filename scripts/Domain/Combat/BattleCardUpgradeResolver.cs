using System;
using System.Linq;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 通用本场升级，保留身份、状态、成长及已走过的冷却进度。
internal static class BattleCardUpgradeResolver
{
    internal static void UpgradeRandomOther(BattleRuntime runtime, PendingAbility pending)
    {
        var candidates = runtime.Cards.Where(card => card.Side == pending.Source.Side && card.EntityId != pending.Source.EntityId
            && !card.Destroyed && !card.IsOnBench && card.Level < 4
            && card.UpgradeLevels.ContainsKey(card.Level) && card.UpgradeLevels.ContainsKey(card.Level + 1)).ToArray();
        if (candidates.Length == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Length)];
        var old = target.UpgradeLevels[target.Level]; var next = target.UpgradeLevels[target.Level + 1];
        foreach (var key in old.Values.Keys.Concat(next.Values.Keys).Distinct())
        {
            var before = old.Values.TryGetValue(key, out var oldValue) ? oldValue : 0;
            var after = next.Values.TryGetValue(key, out var nextValue) ? nextValue : 0;
            target.AddCombatAttribute(key, checked(after - before));
            target.UpgradeBaseChanges[key] = checked((target.UpgradeBaseChanges.TryGetValue(key, out var change) ? change : 0) + after - before);
        }
        var previousAbilities = target.Abilities.ToArray();
        foreach (var definition in next.Abilities.Where(ability => !MatchAbilityRules.IsMatchTrigger(ability.Activation)))
        {
            var state = previousAbilities.FirstOrDefault(ability => ability.Definition.Key == definition.Key);
            if (state is null) { target.Abilities.Add(new BattleAbilityState(definition, target.EffectiveCooldownMultiplier)); continue; }
            var previous = state.Definition;
            var basePrevious = old.Abilities.FirstOrDefault(ability => ability.Key == previous.Key);
            var additional = previous.Effects.Skip(basePrevious?.Effects.Count ?? previous.Effects.Count).ToArray();
            var updated = additional.Length == 0 ? definition : WithEffects(definition, definition.Effects.Concat(additional).ToArray());
            state.RemainingCooldownUnits = Math.Max(0, state.RemainingCooldownUnits
                + BattleCooldownRules.DurationUnits(updated.CooldownTicks, target.CooldownBonusTicks, target.EffectiveCooldownMultiplier)
                - BattleCooldownRules.DurationUnits(previous.CooldownTicks, target.CooldownBonusTicks, target.EffectiveCooldownMultiplier));
            state.Definition = updated;
        }
        // 当前Runtime额外解锁的任务能力保留，正式等级中移除的能力不再发动。
        target.Abilities.RemoveAll(ability => old.Abilities.Any(before => before.Key == ability.Definition.Key)
            && !next.Abilities.Any(after => after.Key == ability.Definition.Key));
        var support = new CardBattleState(new CardBattleSetup(target.EntityId, target.BoardStart, 0, 0,
            target.Abilities.Select(ability => ability.Definition).ToArray(), UseLegacyAttack: false) { CombatValues = target.CombatAttributes }, target.Side);
        target.SupportedCombatAttributes.UnionWith(support.SupportedCombatAttributes);
        var previousLevel = target.Level++;
        target.HasUpgraded = true;
        runtime.Events.Add(new CardUpgradedEvent(runtime.Tick, pending.Source.EntityId, target.EntityId, previousLevel, target.Level));
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        if (!pending.IsEcho && !pending.SuppressEchoes)
            foreach (var source in runtime.AbilitySources.Where(source => source.Side == target.Side && !source.Destroyed && !source.IsOnBench))
            foreach (var ability in source.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.EchoOnAlliedCardUpgraded))
                CombatSimulator.Enqueue(runtime, source, ability, true, eventCard: target);
    }
    private static AbilityDefinition WithEffects(AbilityDefinition definition, EffectDefinition[] effects) => new(definition.Key,
        definition.Activation, definition.Target, definition.ManaCost, definition.CooldownTicks, effects,
        definition.AllowsBench, definition.TriggerStateKey, definition.TriggerCardTag, definition.TriggerCardElement,
        definition.TriggerCardSide, definition.TriggerCardSize, definition.TriggerStatus, targetCardTag: definition.TargetCardTag,
        excludeSourceCard: definition.ExcludeSourceCard, excludedTriggerCardElement: definition.ExcludedTriggerCardElement);
}
