using System;
using System.Collections.Generic;
using System.Linq;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 在战斗 Runtime 累计自身发动并即时解锁通用任务奖励（领域战斗层）。
internal static class BattleQuestResolver
{
    internal static void AdvanceSourceQuests(BattleRuntime runtime, CardBattleState card)
    {
        var changed = false;
        foreach (var quest in card.Quests)
        {
            var progress = card.QuestProgress[quest.Key];
            if (progress >= quest.RequiredCount || !quest.Condition.Matches(new SourceCardActivatedQuestEvent(card.EntityId))) continue;
            card.QuestProgress[quest.Key] = ++progress;
            changed = true;
            runtime.Events.Add(new CardQuestProgressChangedEvent(runtime.Tick, card.EntityId, card.Side,
                quest.Key, progress, card.SummonedCard is null && card.TransformedIdentity is null));
            if (progress == quest.RequiredCount) Unlock(runtime, card, quest);
        }
        if (!changed) return;
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        BattleStateRecorder.CaptureState(runtime);
    }

    private static void Unlock(BattleRuntime runtime, CardBattleState card, CardQuestDefinition quest)
    {
        foreach (var ability in quest.Abilities)
        {
            if (ability.Activation != AbilityActivation.PassiveWhileEnabled)
            {
                card.Abilities.Add(new BattleAbilityState(ability, card.EffectiveCooldownMultiplier));
                continue;
            }
            foreach (var modifier in ability.Effects.Cast<ModifyAttributeEffectDefinition>())
            {
                if (!card.SupportsCombatAttribute(modifier.AttributeKey)) continue;
                var current = card.AddCombatAttribute(modifier.AttributeKey, modifier.Amount);
                runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, card.EntityId,
                    modifier.AttributeKey, modifier.Amount, current));
            }
        }
        if (quest.AdditionalActiveEffects.Count > 0)
            foreach (var ability in card.Abilities.Where(item => item.Definition.Activation == AbilityActivation.Active))
            {
                var definition = ability.Definition;
                ability.Definition = new AbilityDefinition(definition.Key, definition.Activation, definition.Target,
                    definition.ManaCost, definition.CooldownTicks,
                    Array.AsReadOnly(definition.Effects.Concat(quest.AdditionalActiveEffects).ToArray()),
                    definition.AllowsBench, definition.TriggerStateKey, definition.TriggerCardTag,
                    definition.TriggerCardElement, definition.TriggerCardSide);
            }
        if (quest.UnlockedElementKeys.Count > 0) card.ElementKeys = new HashSet<Godot.StringName>(quest.UnlockedElementKeys);
        if (quest.UnlockedTags.Count > 0) card.Tags = new TagSet(card.Tags.Concat(quest.UnlockedTags));
    }
}
