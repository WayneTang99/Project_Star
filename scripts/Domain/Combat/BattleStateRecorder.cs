using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 冻结战斗状态并生成结果，不推进规则或随机（领域战斗层内部）。
internal static class BattleStateRecorder
{
    // 记录结束事件与最终快照，返回 Runtime 的只读结算记录。
    internal static BattleResult CreateResult(BattleRuntime runtime, BattleOutcome outcome, BattleEndReason reason)
    {
        runtime.Events.Add(new BattleEndedEvent(runtime.Tick, reason));
        CaptureState(runtime);
        return new BattleResult(outcome, reason, runtime.Tick, runtime.PlayerHero.Health, runtime.OpponentHero.Health,
            runtime.Events.AsReadOnly(), runtime.PermanentChanges.AsReadOnly(), runtime.States.AsReadOnly(),
            runtime.PermanentAttributeBonuses.AsReadOnly());
    }

    // 只记录战斗层已经计算出的结果，不推进冷却、随机数或任何玩法状态。
    internal static void CaptureState(BattleRuntime runtime)
    {
        var cards = runtime.Cards.Select(card => new CardBattleSnapshot(card.EntityId, card.Side, card.Destroyed,
            card.HasteDuration, card.SlowDuration, card.ImmobilizeDuration,
            Array.AsReadOnly(card.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.Active)
                .Select(ability => ability.RemainingCooldownUnits).ToArray()),
            new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(card.CombatAttributes.Keys
                .Concat(BattleEffectResolver.AlliedAttributeAuras(runtime, card).Select(aura => aura.AttributeKey)).Distinct()
                .ToDictionary(key => key, key => key == GameAttributeKeys.Multicast
                    ? BattleEffectResolver.GetEffectiveMulticast(runtime, card) : BattleEffectResolver.GetEffectiveCombatAttribute(runtime, card, key))))
            {
                CooldownDurationUnits = Array.AsReadOnly(card.Abilities
                    .Where(ability => ability.Definition.Activation == AbilityActivation.Active)
                    .Select(ability => (ability.Definition.CooldownTicks + card.CooldownBonusTicks) * 2
                        * card.EffectiveCooldownMultiplier).ToArray()),
                IsFlying = card.IsFlying, IsBerserk = card.IsBerserk, IsOnBench = card.IsOnBench,
                TransformedIdentity = card.TransformedIdentity, Level = card.Level,
                SummonedCard = card.SummonedCard,
                Quests = Array.AsReadOnly(card.Quests.Select(quest => new CardQuestBattleSnapshot(quest.Key,
                    card.QuestProgress[quest.Key], quest.RequiredCount, card.QuestProgress[quest.Key] >= quest.RequiredCount)).ToArray()),
                QuestDefinitions = Array.AsReadOnly(card.Quests.ToArray()),
                QuestElementKeys = Array.AsReadOnly(card.ElementKeys.OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray()),
                QuestTags = Array.AsReadOnly(card.Tags.OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray()),
                QuestAbilities = card.Quests.Count == 0 ? Array.Empty<AbilityDefinition>()
                    : Array.AsReadOnly(card.Abilities.Select(ability => ability.Definition).ToArray()),
                TransformedTags = card.TransformedIdentity is null ? Array.Empty<StringName>()
                    : Array.AsReadOnly(card.Tags.ToArray()),
                TransformedAbilities = card.TransformedIdentity is null ? Array.Empty<AbilityDefinition>()
                    : Array.AsReadOnly(card.Abilities.Select(ability => ability.Definition).ToArray())
            }).ToArray();
        runtime.States.Add(new BattleStateSnapshot(runtime.Tick, runtime.Events.Count,
            runtime.Tick.Value >= runtime.EclipseTime.Value, Hero(runtime.PlayerHero), Hero(runtime.OpponentHero),
            Array.AsReadOnly(cards)));

        static HeroBattleSnapshot Hero(HeroBattleState hero) => new(hero.Health, hero.MaxHealth, hero.Armor,
            hero.Mana, hero.MaxMana, hero.Burn, hero.Poison, hero.HealthRegen, hero.ManaRegen);
    }


}
