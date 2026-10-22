using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 通用效果、伤害、光环属性、充能与摧毁结算（领域战斗层内部）。
internal static class BattleEffectResolver
{
    // 累加当前有效光环提供的多重，不修改来源或目标属性。
    internal static int GetEffectiveMulticast(BattleRuntime runtime, CardBattleState card)
    {
        var multicast = card.GetCombatAttribute(GameAttributeKeys.Multicast);
        if (!card.Destroyed && !card.IsOnBench)
            foreach (var ability in card.Abilities)
            foreach (var effect in ability.Definition.Effects)
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantSourceMulticastWhileEnemyTaggedEffectDefinition conditional
                    && runtime.Cards.Any(enemy => enemy.Side != card.Side && !enemy.Destroyed && !enemy.IsOnBench
                        && ContainsAnyEffectiveTag(runtime, enemy, conditional.RequiredAnyTags)))
                    multicast = checked(multicast + conditional.Amount);
        if (!card.Destroyed && !card.IsOnBench)
            foreach (var ability in card.Abilities)
            foreach (var effect in ability.Definition.Effects)
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition alliedConditional
                    && runtime.Cards.Any(ally => ally.Side == card.Side && !ally.Destroyed && !ally.IsOnBench
                        && (alliedConditional.RequiredTag is not { } tag || HasEffectiveTag(runtime, ally, tag))
                        && (alliedConditional.RequiredElement is not { } element || ally.ElementKeys.Contains(element))))
                    multicast = checked(multicast + alliedConditional.Amount);
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side != card.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                foreach (var effect in ability.Definition.Effects)
                {
                    if (effect is GrantMulticastToAlliedElementCardsEffectDefinition aura
                        && card.ElementKeys.Contains(aura.ElementKey))
                    {
                        multicast = checked(multicast + aura.Amount);
                    }
                }
            }
        }
        return multicast;
    }

    // 按通用效果定义提交当前 Runtime 变化，保持原事件发布顺序。
    internal static void ApplyEffect(BattleRuntime runtime, PendingAbility pending, EffectDefinition effect)
    {
        var definition = pending.Ability.Definition;
        var targetSide = definition.Target == AbilityTarget.EnemyHero
            ? Opposite(pending.Source.Side) : pending.Source.Side;
        var hero = runtime.GetHero(targetSide);
        switch (effect)
        {
            case ElementSequenceEffectDefinition { Status: { } status } sequence:
                ApplyEffect(runtime, pending, new ApplyStatusToRandomEnemyCardEffectDefinition(status, sequence.StatusTicks));
                break;
            case SacrificeSourceAndNeighborsToSummonEffectDefinition sacrificeSummon when pending.Source is CardBattleState sacrificeSource:
                var neighbors = runtime.Cards.Where(card => card.Side == sacrificeSource.Side && !card.Destroyed && !card.IsOnBench
                    && card.EntityId != sacrificeSource.EntityId
                    && (card.BoardStart + card.OccupiedSlots == sacrificeSource.BoardStart
                        || card.BoardStart == sacrificeSource.BoardStart + sacrificeSource.OccupiedSlots)).ToArray();
                foreach (var neighbor in neighbors)
                    DestroyCard(runtime, neighbor, sacrificeSource.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                DestroyCard(runtime, sacrificeSource, sacrificeSource.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                foreach (var side in new[] { AdjacentCardSide.Left, AdjacentCardSide.Right })
                {
                    if (!CanSummonAdjacentCard(runtime, sacrificeSource, sacrificeSummon.Card, side)) continue;
                    var start = side == AdjacentCardSide.Left
                        ? sacrificeSource.BoardStart - sacrificeSummon.Card.Attributes.Identity.OccupiedSlots
                        : sacrificeSource.BoardStart + sacrificeSource.OccupiedSlots;
                    var summoned = SummonCard(runtime, sacrificeSource, sacrificeSummon.Card, sacrificeSource.Level, start, pending.IsEcho || pending.SuppressEchoes);
                    if (summoned is not null && sacrificeSummon.SummonedStateKey is { } stateKey)
                        SetCardState(runtime, pending, summoned, stateKey, true);
                    break;
                }
                break;
            case GrantTagToRandomEnemyCardsEffectDefinition randomTag:
                var tagCandidates = runtime.Cards.Where(card => card.Side != pending.Source.Side
                    && !card.Destroyed && !card.IsOnBench).ToList();
                for (var index = 0; index < randomTag.TargetCount && tagCandidates.Count > 0; index++)
                {
                    var chosenIndex = runtime.NextRandomIndex(tagCandidates.Count);
                    var chosen = tagCandidates[chosenIndex];
                    tagCandidates.RemoveAt(chosenIndex);
                    chosen.Tags = new TagSet(chosen.Tags.Concat(new[] { randomTag.Tag }));
                }
                break;
            case EnterEclipseEffectDefinition:
                CombatSimulator.EnterEclipse(runtime);
                break;
            case IncreaseHeroMaxHealthPercentEffectDefinition healthGrowth:
                var healthIncrease = checked((int)((long)hero.MaxHealth * healthGrowth.Percent / 100));
                hero.MaxHealth = checked(hero.MaxHealth + healthIncrease);
                hero.Health = checked(hero.Health + healthIncrease);
                break;
            case IncreaseHeroMaxHealthEffectDefinition fixedHealthGrowth:
                var growingHero = fixedHealthGrowth.TargetsSourceHero ? runtime.GetHero(pending.Source.Side) : hero;
                growingHero.MaxHealth = checked(growingHero.MaxHealth + fixedHealthGrowth.Amount);
                growingHero.Health = checked(growingHero.Health + fixedHealthGrowth.Amount);
                break;
            case ApplyPrimaryEffectPercentStatusEffectDefinition primaryStatus:
                var extraAmount = primaryStatus.CalculateAmount(pending.Ability.Definition.Effects.FirstOrDefault(), pending.PrimaryValue);
                if (primaryStatus.Status is BattleStatus.Burn or BattleStatus.Poison)
                    extraAmount = checked(extraAmount + GetEffectiveCombatAttribute(runtime, pending.Source,
                        primaryStatus.Status == BattleStatus.Burn ? GameAttributeKeys.BurnBonus : GameAttributeKeys.PoisonBonus));
                var enemyHero = runtime.GetHero(Opposite(pending.Source.Side));
                var extraApplied = BattleStatusResolver.ApplyStatus(pending.Source, enemyHero,
                    new ApplyStatusEffectDefinition(primaryStatus.Status, BerserkAmount(pending, extraAmount)));
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, primaryStatus.Status, extraApplied));
                break;
            case SummonAtDestroyedCardEffectDefinition replacement:
                if ((pending.EventCard ?? (definition.Activation == AbilityActivation.EchoOnSourceDestroyed
                        ? pending.Source as CardBattleState : null)) is { Destroyed: true, IsOnBench: false } destroyedCard
                    && destroyedCard.Side == pending.Source.Side)
                    for (var index = 0; index < replacement.Count; index++)
                        SummonCard(runtime, pending.Source, replacement.Card,
                            replacement.UseSourceLevel && pending.Source is CardBattleState replacementSource
                                ? replacementSource.Level : destroyedCard.Level,
                            destroyedCard.BoardStart + index * replacement.Card.Attributes.Identity.OccupiedSlots,
                            pending.IsEcho || pending.SuppressEchoes);
                break;
            case SummonRandomMatchingCardEffectDefinition matchingSummon when pending.Source is CardBattleState randomSource:
                var summonStart = Enumerable.Range(0, runtime.GetBattlefieldCapacity(randomSource.Side))
                    .FirstOrDefault(start => start + (int)matchingSummon.Size <= runtime.GetBattlefieldCapacity(randomSource.Side)
                        && !runtime.Cards.Any(card => card.Side == randomSource.Side && !card.IsOnBench && !card.Destroyed
                            && card.BoardStart < start + (int)matchingSummon.Size && start < card.BoardStart + card.OccupiedSlots), -1);
                if (summonStart < 0) break;
                var summonPool = runtime.GetCardDefinitions(randomSource.Side).Where(card => card.SupportsLevel(randomSource.Level)
                    && card.Attributes.Identity.Size == matchingSummon.Size && card.Attributes.Identity.ElementKeys.Contains(matchingSummon.ElementKey)
                    && (card.Attributes.Identity.FactionKey == randomSource.Identity?.FactionKey || card.Attributes.Identity.FactionKey == GameFactions.Neutral))
                    .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
                if (summonPool.Length > 0)
                    SummonCard(runtime, randomSource, summonPool[runtime.NextRandomIndex(summonPool.Length)], randomSource.Level,
                        summonStart, pending.IsEcho || pending.SuppressEchoes);
                break;
            case SummonAdjacentCardEffectDefinition summon:
                if (pending.Source is not CardBattleState summoner)
                    throw new InvalidOperationException("Adjacent summon requires a card source.");
                SummonAdjacentCard(runtime, summoner, summon, pending.IsEcho || pending.SuppressEchoes);
                break;
            case SummonIntoAllEmptySlotsEffectDefinition fill when pending.Source is CardBattleState fillSource:
                for (var start = 0; start + fill.Card.Attributes.Identity.OccupiedSlots <= runtime.GetBattlefieldCapacity(fillSource.Side); start++)
                    SummonCard(runtime, fillSource, fill.Card, fillSource.Level, start, pending.IsEcho || pending.SuppressEchoes);
                break;
            case SummonRandomAdjacentCardEffectDefinition randomSummon:
                if (pending.Source is not CardBattleState randomSummoner)
                    throw new InvalidOperationException("Adjacent summon requires a card source.");
                var templates = randomSummon.Cards.Where(card => CanSummonAdjacentCard(runtime,
                    randomSummoner, card, randomSummon.Side)).ToArray();
                if (templates.Length > 0)
                    SummonAdjacentCard(runtime, randomSummoner,
                        new SummonAdjacentCardEffectDefinition(templates[runtime.NextRandomIndex(templates.Length)], randomSummon.Side), pending.IsEcho || pending.SuppressEchoes);
                break;
            case IncreaseSourceCardAttributeEffectDefinition increase:
                if (pending.Source is not CardBattleState attributeSource)
                    throw new InvalidOperationException("Source attribute increase requires a card source.");
                if (!attributeSource.SupportsCombatAttribute(increase.AttributeKey)) break;
                var increasedValue = attributeSource.AddCombatAttribute(increase.AttributeKey, increase.Amount);
                runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, attributeSource.EntityId,
                    increase.AttributeKey, increase.Amount, increasedValue));
                if (increase.Permanent && attributeSource.SummonedCard is null && attributeSource.TransformedIdentity is null)
                    runtime.PermanentAttributeBonuses.Add(new PermanentCardAttributeBonus(
                        attributeSource.EntityId, attributeSource.Side, increase.AttributeKey, increase.Amount));
                break;
            case MultiplySourceCardAttributeEffectDefinition multiplier:
                if (pending.Source is not CardBattleState multipliedSource)
                    throw new InvalidOperationException("Source attribute multiplier requires a card source.");
                if (!multipliedSource.SupportsCombatAttribute(multiplier.AttributeKey)) break;
                var contribution = checked(multipliedSource.GetCombatAttribute(multiplier.AttributeKey) * (multiplier.Multiplier - 1));
                var multipliedValue = multipliedSource.AddCombatAttribute(multiplier.AttributeKey, contribution);
                runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, multipliedSource.EntityId,
                    multiplier.AttributeKey, contribution, multipliedValue));
                break;
            case RandomizeSourceElementEffectDefinition when pending.Source is CardBattleState elementalSource:
                var elements = GameElements.All.Where(element => element != GameElements.General).ToArray();
                elementalSource.ElementKeys = new HashSet<StringName> { elements[runtime.NextRandomIndex(elements.Length)] };
                BattleStatusResolver.RefreshCooldownAuras(runtime);
                break;
            case TransformRandomEnemyCardEffectDefinition transform:
                TransformRandomEnemyCard(runtime, pending.Source, transform);
                break;
            case TransformSourceIntoRandomCardsEffectDefinition split when pending.Source is CardBattleState splitSource:
                TransformSourceIntoRandomCards(runtime, splitSource, split, pending.IsEcho || pending.SuppressEchoes);
                break;
            case SkillCountMaxManaEffectDefinition manaSkills:
                hero.MaxMana = checked(hero.MaxMana + runtime.Skills.Count(skill => skill.Side == pending.Source.Side) * manaSkills.Multiplier);
                break;
            case IncreaseHeroMaxManaEffectDefinition maxMana:
                hero.MaxMana = checked(hero.MaxMana + maxMana.Amount);
                break;
            case RestoreEventManaSpentEffectDefinition:
                ApplyEffect(runtime, pending, new RestoreManaEffectDefinition(pending.EventManaSpent));
                break;
            case HeroManaDamageEffectDefinition manaDamage:
                var manaHero = runtime.GetHero(pending.Source.Side);
                var manaDamageAmount = checked(manaHero.MaxMana + manaHero.ManaRegen * manaDamage.RegenMultiplier
                    + GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.AttackDamage));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, ActivationDamageAmount(pending, manaDamageAmount), false,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SkillCountDamageEffectDefinition skillDamage:
                var skillAmount = checked(runtime.Skills.Count(skill => skill.Side == pending.Source.Side) * skillDamage.Multiplier
                    + GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.AttackDamage));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, ActivationDamageAmount(pending, skillAmount), false,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case DamageEffectDefinition damage:
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, ActivationDamageAmount(pending, damage.Amount), damage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroLevelScaledDamageEffectDefinition levelDamage:
                var sourceLevel = runtime.GetHero(pending.Source.Side).Level;
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero,
                    ActivationDamageAmount(pending, checked(sourceLevel * levelDamage.Multiplier)), levelDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case MaxHealthPercentDamageEffectDefinition percentDamage:
                var amount = checked((int)((long)hero.MaxHealth * percentDamage.Percent / 100));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, ActivationDamageAmount(pending, amount), percentDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case AttributeDamageEffectDefinition attributeDamage:
                if (ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    ActivationDamageAmount(pending, AttributeDamageAmount(runtime, pending, attributeDamage)),
                    attributeDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroHealthScaledAttributeDamageEffectDefinition scaledDamage:
                var healthSourceHero = runtime.GetHero(pending.Source.Side);
                var scaledAmount = checked((int)((long)GetEffectiveCombatAttribute(
                    runtime, pending.Source, scaledDamage.AttributeKey) * healthSourceHero.Health / healthSourceHero.MaxHealth));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, ActivationDamageAmount(pending, scaledAmount),
                    scaledDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroArmorDamageEffectDefinition heroArmorDamage:
                var armorSourceHero = runtime.GetHero(pending.Source.Side);
                var bonusDamage = heroArmorDamage.BonusAttributeKey is not { IsEmpty: false } bonusAttributeKey
                    ? 0
                    : GetEffectiveCombatAttribute(runtime, pending.Source, bonusAttributeKey);
                var consumedArmor = armorSourceHero.Armor;
                var armorDamageAmount = ActivationDamageAmount(pending, checked((consumedArmor + bonusDamage) * heroArmorDamage.Multiplier));
                if (heroArmorDamage.ConsumeArmor) armorSourceHero.Armor -= consumedArmor;
                if (ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    armorDamageAmount,
                    heroArmorDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case ReduceHeroStatusEffectDefinition reduction:
                var removed = Math.Min(reduction.Amount, reduction.Status == BattleStatus.Burn ? hero.Burn : hero.Poison);
                if (reduction.Status == BattleStatus.Burn) hero.Burn -= removed;
                else hero.Poison -= removed;
                if (removed > 0) runtime.Events.Add(new StatusChangedEvent(runtime.Tick, reduction.Status, -removed));
                break;
            case HealEffectDefinition heal:
                if (!CanHeal(runtime, pending.Source.Side, heal)) break;
                var healing = HealingAmount(runtime, pending.Source, heal);
                var healingHero = heal.TargetsSourceHero ? runtime.GetHero(pending.Source.Side) : hero;
                healingHero.Health = (int)Math.Min(healingHero.MaxHealth, (long)healingHero.Health + healing);
                break;
            case HealMaxHealthPercentEffectDefinition percentHeal:
                var percentHealing = checked((int)((long)hero.MaxHealth * percentHeal.Percent / 100)
                    + GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.HealingBonus));
                hero.Health = (int)Math.Min(hero.MaxHealth, (long)hero.Health + percentHealing);
                break;
            case GainSourceHeroStealthEffectDefinition stealth:
                var stealthHero = runtime.GetHero(pending.Source.Side);
                stealthHero.StealthDurationTicks = checked(stealthHero.StealthDurationTicks + stealth.DurationTicks);
                runtime.Events.Add(new HeroStealthChangedEvent(runtime.Tick, pending.Source.Side, stealthHero.StealthDurationTicks));
                break;
            case RestoreManaEffectDefinition restoreMana:
                var restored = (int)System.Math.Min(restoreMana.Amount, (long)hero.MaxMana - hero.Mana);
                hero.Mana += restored;
                runtime.Events.Add(new ManaChangedEvent(runtime.Tick, targetSide, restored, hero.Mana));
                break;
            case ArmorEffectDefinition armor:
                hero.Armor = checked(hero.Armor + armor.Amount);
                break;
            case GainSourceHeroArmorEffectDefinition sourceArmor:
                var alliedHero = runtime.GetHero(pending.Source.Side);
                alliedHero.Armor = checked(alliedHero.Armor + sourceArmor.Amount);
                break;
            case GainSourceHeroLevelScaledArmorEffectDefinition levelArmor:
                var levelArmorHero = runtime.GetHero(pending.Source.Side);
                levelArmorHero.Armor = checked(levelArmorHero.Armor + checked(levelArmorHero.Level * levelArmor.Multiplier));
                break;
            case GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor:
                var armorTarget = runtime.GetHero(pending.Source.Side);
                armorTarget.Armor = checked(
                    armorTarget.Armor + GetEffectiveCombatAttribute(runtime, pending.Source, attributeArmor.AttributeKey));
                break;
            case GainArmorEqualToManaSpentEffectDefinition:
                var sourceHero = runtime.GetHero(pending.Source.Side);
                sourceHero.Armor = checked(sourceHero.Armor + sourceHero.ManaSpent);
                break;
            case ApplyAttributeStatusEffectDefinition attributeStatus:
                ApplyEffect(runtime, pending, new ApplyStatusEffectDefinition(attributeStatus.Status,
                    pending.Source.GetCombatAttribute(attributeStatus.AttributeKey)));
                break;
            case ApplySourceAttributePercentStatusEffectDefinition percentStatus:
                var statusAmount = checked((int)((long)GetEffectiveCombatAttribute(runtime, pending.Source,
                    percentStatus.AttributeKey) * percentStatus.Percent / 100));
                ApplyEffect(runtime, pending, new ApplyStatusEffectDefinition(percentStatus.Status, statusAmount));
                break;
            case ApplyStatusEffectDefinition status:
                if (status.Status is BattleStatus.Burn or BattleStatus.Poison)
                    status = status with { Amount = checked(status.Amount
                        + GetEffectiveCombatAttribute(runtime, pending.Source,
                            status.Status == BattleStatus.Burn ? GameAttributeKeys.BurnBonus : GameAttributeKeys.PoisonBonus)) };
                var appliedStatus = status.Status is BattleStatus.Burn or BattleStatus.Poison
                    ? status with { Amount = BerserkAmount(pending, status.Amount) }
                    : status with { Amount = BattleStatusResolver.DurationWithSourceBonus(runtime, pending, status.Status, status.Amount) };
                var statusTarget = definition.Target is AbilityTarget.LeftAdjacentAlliedCard or AbilityTarget.RightAdjacentAlliedCard or AbilityTarget.RightmostAlliedCard
                    ? GetTargetCard(runtime, pending)
                    : definition.Target == AbilityTarget.EventCard ? pending.EventCard : pending.Source;
                if (statusTarget is null) break;
                var statusHero = status.TargetsSourceHero ? runtime.GetHero(pending.Source.Side) : hero;
                var appliedAmount = BattleStatusResolver.ApplyStatus(statusTarget, statusHero, appliedStatus);
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, status.Status, appliedAmount));
                if (appliedAmount > 0 && status.Status == BattleStatus.ManaRegen
                    && definition.Activation == AbilityActivation.Active && !pending.IsEcho && !pending.SuppressEchoes)
                    foreach (var regenSource in runtime.AbilitySources.Where(source => source.Side == (status.TargetsSourceHero ? pending.Source.Side : targetSide) && !source.Destroyed && !source.IsOnBench))
                    foreach (var regenAbility in regenSource.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.EchoOnAlliedManaRegenGained))
                        CombatSimulator.Enqueue(runtime, regenSource, regenAbility, true);
                if (appliedAmount > 0 && statusTarget is CardBattleState statusCard)
                {
                    BattleStatusResolver.ApplyAdjacentStatusReactions(runtime, statusCard, status.Status);
                    if (!pending.IsEcho) CombatSimulator.EnqueueSourceStatusGainedEchoes(runtime, statusCard, status.Status);
                }
                if (appliedAmount > 0 && status.Status == BattleStatus.SlowDuration && !pending.IsEcho)
                    CombatSimulator.EnqueueSlowAppliedEchoes(runtime, pending.Source.Side);
                break;
            case TriggerOtherAlliedElementActivesEffectDefinition trigger:
                var chain = pending.TriggerChain ?? new HashSet<EntityId> { pending.Source.EntityId };
                foreach (var candidate in runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && card.EntityId != pending.Source.EntityId && !card.Destroyed && !card.IsOnBench
                    && card.ElementKeys.Contains(trigger.ElementKey)
                    && card.Abilities.Any(ability => ability.Definition.Activation == AbilityActivation.Active)))
                {
                    if (!chain.Add(candidate.EntityId)) continue;
                    foreach (var active in candidate.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.Active))
                        CombatSimulator.Enqueue(runtime, candidate, active, false, triggeredActivation: true, triggerChain: chain);
                }
                break;
            case ActivateSourceCardEffectDefinition:
                if (pending.Source is CardBattleState sourceCard)
                    foreach (var active in sourceCard.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.Active))
                        CombatSimulator.Enqueue(runtime, sourceCard, active, false, triggeredActivation: true,
                            allowDestroyedSource: pending.AllowDestroyedSource, triggerChain: pending.TriggerChain);
                break;
            case SetSourceCardStateEffectDefinition state:
                if (pending.Source is not CardBattleState stateSource)
                    throw new InvalidOperationException("Card state requires a card source.");
                SetCardState(runtime, pending, stateSource, state.StateKey, state.Enabled);
                break;
            case SetRandomAdjacentAlliedTaggedCardStateEffectDefinition adjacentState:
                if (pending.Source is not CardBattleState adjacentStateSource)
                    throw new InvalidOperationException("Adjacent card state requires a card source.");
                var candidates = runtime.Cards.Where(card => card.Side == adjacentStateSource.Side
                    && card.EntityId != adjacentStateSource.EntityId && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, adjacentState.RequiredTag)
                    && (card.BoardStart + card.OccupiedSlots == adjacentStateSource.BoardStart
                        || card.BoardStart == adjacentStateSource.BoardStart + adjacentStateSource.OccupiedSlots)).ToArray();
                if (candidates.Length > 0)
                    SetCardState(runtime, pending, candidates[runtime.NextRandomIndex(candidates.Length)],
                        adjacentState.StateKey, adjacentState.Enabled);
                break;
            case SetRandomOtherAlliedCardStateEffectDefinition otherState:
                var otherCandidates = runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && card.EntityId != pending.Source.EntityId && !card.Destroyed && !card.IsOnBench).ToArray();
                if (otherCandidates.Length > 0)
                    SetCardState(runtime, pending, otherCandidates[runtime.NextRandomIndex(otherCandidates.Length)],
                        otherState.StateKey, otherState.Enabled);
                break;
            case ApplyStatusToAdjacentAlliedCardsEffectDefinition adjacent:
                if (pending.Source is not CardBattleState adjacentSource)
                    throw new InvalidOperationException("Adjacent card effects require a card source.");
                BattleStatusResolver.ApplyStatusToAdjacentAlliedCards(runtime, pending, adjacentSource, adjacent);
                break;
            case ApplyStatusToRandomEnemyCardEffectDefinition randomStatus:
                BattleStatusResolver.ApplyStatusToRandomEnemyCard(runtime, pending, randomStatus);
                break;
            case ApplyStatusToRandomAlliedCardEffectDefinition alliedStatus:
                BattleStatusResolver.ApplyStatusToRandomAlliedCard(runtime, pending, alliedStatus);
                break;
            case ChargeRandomOtherAlliedElementCardEffectDefinition charge:
                if (pending.Source is not CardBattleState chargeSource)
                    throw new InvalidOperationException("Random card charge requires a card source.");
                ChargeRandomOtherAlliedElementCard(runtime, pending, chargeSource, charge);
                break;
            case ChargeSourceCardEffectDefinition sourceCharge:
                if (pending.Source is not CardBattleState selfChargeSource)
                    throw new InvalidOperationException("Source card charge requires a card source.");
                ChargeCard(runtime, selfChargeSource, selfChargeSource, sourceCharge.AmountTicks, pending.IsEcho || pending.SuppressEchoes);
                break;
            case ChargeRandomOtherAlliedCardsEffectDefinition randomCharge:
                var randomChargeSource = pending.Source;
                var chargeCandidates = runtime.Cards.Where(card => card.Side == randomChargeSource.Side
                    && (!randomCharge.ExcludeSource || card.EntityId != randomChargeSource.EntityId) && !card.Destroyed && !card.IsOnBench
                    && (randomCharge.RequiredTag is null || HasEffectiveTag(runtime, card, randomCharge.RequiredTag))
                    && card.Abilities.Any(ability => ability.Definition.Activation == AbilityActivation.Active
                        && ability.RemainingCooldownUnits > 0)).ToList();
                for (var i = 0; i < randomCharge.TargetCount && chargeCandidates.Count > 0; i++)
                {
                    var index = runtime.NextRandomIndex(chargeCandidates.Count);
                    ChargeCard(runtime, randomChargeSource, chargeCandidates[index], randomCharge.AmountTicks, pending.IsEcho || pending.SuppressEchoes);
                    chargeCandidates.RemoveAt(index);
                }
                break;
            case ChargeCardEffectDefinition cardCharge:
                if (pending.Source is not CardBattleState cardChargeSource)
                    throw new InvalidOperationException("Card charge requires a card source.");
                var chargeTarget = GetTargetCard(runtime, pending);
                if (chargeTarget is not null)
                    ChargeCard(runtime, cardChargeSource, chargeTarget, cardCharge.AmountTicks, pending.IsEcho || pending.SuppressEchoes);
                break;
            case ChargeTaggedAlliedCardsEffectDefinition taggedCharge:
                foreach (var target in runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed
                    && !card.IsOnBench && HasEffectiveTag(runtime, card, taggedCharge.RequiredTag)
                    && (!taggedCharge.ExcludeEventCard || card.EntityId != pending.EventCard?.EntityId)).ToArray())
                    ChargeCard(runtime, pending.Source, target, taggedCharge.AmountTicks, pending.IsEcho || pending.SuppressEchoes);
                break;
            case RepairRandomDestroyedAlliedCardEffectDefinition repair:
                RepairRandomDestroyedAlliedCard(runtime, pending, repair);
                break;
            case DestroyCardEffectDefinition destroy:
                if (pending.Source is not CardBattleState destroySource)
                    throw new InvalidOperationException("Self-destruction requires a card source.");
                DestroyCard(runtime, destroySource, pending.Source.EntityId, destroy.Permanent, pending.IsEcho || pending.SuppressEchoes);
                break;
            case DestroyAdjacentAlliedCardsEffectDefinition adjacentDestroy:
                if (pending.Source is not CardBattleState adjacentDestroySource)
                    throw new InvalidOperationException("Adjacent destruction requires a card source.");
                var adjacentTargets = runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && !card.Destroyed && !card.IsOnBench && card.EntityId != pending.Source.EntityId
                    && card.OccupiedSlots == (int)adjacentDestroy.Size
                    && (card.BoardStart + card.OccupiedSlots == adjacentDestroySource.BoardStart
                        || card.BoardStart == adjacentDestroySource.BoardStart + adjacentDestroySource.OccupiedSlots)).ToArray();
                foreach (var target in adjacentTargets)
                    DestroyCard(runtime, target, pending.Source.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                break;
            case DestroyBattlefieldCardsEffectDefinition battlefieldDestroy:
                foreach (var target in runtime.Cards.Where(card => !card.Destroyed && !card.IsOnBench
                    && card.OccupiedSlots <= (int)battlefieldDestroy.MaximumSize).ToArray())
                    DestroyCard(runtime, target, pending.Source.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                break;
            case DestroyRandomEnemyCardEffectDefinition randomDestroy:
                DestroyRandomEnemyCard(runtime, pending.Source, randomDestroy, pending.IsEcho || pending.SuppressEchoes);
                break;
            case DestroyAlliedCardsEffectDefinition alliedDestroy:
                foreach (var target in runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                    && card.OccupiedSlots == (int)alliedDestroy.Size && card.ElementKeys.Contains(alliedDestroy.ElementKey)).ToArray())
                    DestroyCard(runtime, target, pending.Source.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                break;
            case DestroyAlliedCardsAndDamageEffectDefinition sacrifice:
                var sacrifices = runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                    && card.OccupiedSlots == (int)sacrifice.Size && card.ElementKeys.Contains(sacrifice.ElementKey)).ToArray();
                var sacrificeDamage = ActivationDamageAmount(pending, GetEffectiveCombatAttribute(runtime, pending.Source, sacrifice.DamageAttributeKey));
                foreach (var sacrificed in sacrifices)
                    DestroyCard(runtime, sacrificed, pending.Source.EntityId, false, pending.IsEcho || pending.SuppressEchoes);
                foreach (var sacrificed in sacrifices)
                {
                    if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, sacrificeDamage, false, GetDamageSourceKind(pending.Source))
                        && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                }
                break;
            case IncreaseSourceCooldownEffectDefinition cooldown:
                if (!cooldown.FirstActivationOnly || pending.Source.ActivationCount == 0)
                    AdjustSourceCooldown(pending.Source, cooldown.AmountTicks);
                break;
            case ReduceSourceCooldownEffectDefinition reduction:
                AdjustSourceCooldown(pending.Source, -reduction.AmountTicks);
                break;
            case UpgradeRandomOtherAlliedCardEffectDefinition:
                BattleCardUpgradeResolver.UpgradeRandomOther(runtime, pending);
                break;
            case ReduceEventCardCooldownPercentEffectDefinition reduction when pending.EventCard is { Destroyed: false } upgraded:
                var oldMultiplier = upgraded.EffectiveCooldownMultiplier;
                upgraded.CooldownMultiplier *= 1m - reduction.Percent / 100m;
                foreach (var ability in upgraded.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.Active))
                    ability.RemainingCooldownUnits = Math.Max(0, ability.RemainingCooldownUnits
                        + BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, upgraded.CooldownBonusTicks, upgraded.EffectiveCooldownMultiplier)
                        - BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, upgraded.CooldownBonusTicks, oldMultiplier));
                break;
            case ModifyTaggedAlliedCardsAttributeEffectDefinition modifier:
                var targets = runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                    && (!modifier.AdjacentOnly || pending.Source is CardBattleState adjacentModifierSource
                        && card.EntityId != adjacentModifierSource.EntityId
                        && (card.BoardStart + card.OccupiedSlots == adjacentModifierSource.BoardStart
                            || adjacentModifierSource.BoardStart + adjacentModifierSource.OccupiedSlots == card.BoardStart))
                    && HasEffectiveTag(runtime, card, modifier.RequiredTag) && (modifier.RequiresAttributeSupport
                        ? card.SupportsCombatAttribute(modifier.AttributeKey)
                        : card.CombatAttributes.ContainsKey(modifier.AttributeKey))).ToArray();
                if (modifier.RandomSingleTarget && targets.Length > 0)
                    targets = [targets[runtime.NextRandomIndex(targets.Length)]];
                foreach (var card in targets)
                {
                    var currentValue = card.AddCombatAttribute(modifier.AttributeKey, modifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(
                        runtime.Tick,
                        card.EntityId,
                        modifier.AttributeKey,
                        modifier.Amount,
                        currentValue));
                }
                break;
            case ModifyAlliedAttackCardsAttributeEffectDefinition allAttackModifier:
                foreach (var card in runtime.Cards)
                {
                    if (card.Side != pending.Source.Side || card.Destroyed || card.IsOnBench || !card.IsAttackCard) continue;
                    var currentValue = card.AddCombatAttribute(allAttackModifier.AttributeKey, allAttackModifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, card.EntityId,
                        allAttackModifier.AttributeKey, allAttackModifier.Amount, currentValue));
                }
                break;
            case ModifyRandomAlliedAttackCardAttributeEffectDefinition randomModifier:
                var attackers = runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && !card.Destroyed && !card.IsOnBench && card.IsAttackCard).ToArray();
                if (attackers.Length > 0)
                {
                    var card = attackers[runtime.NextRandomIndex(attackers.Length)];
                    var currentValue = card.AddCombatAttribute(randomModifier.AttributeKey, randomModifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, card.EntityId,
                        randomModifier.AttributeKey, randomModifier.Amount, currentValue));
                }
                break;
        }
    }

    // 只放大狂暴来源的发动数值，整数结果向下取整，不改变来源属性。
    // 读取当前来源数值一次，后续护甲、治疗或摧毁不会改变本次主值。
    internal static int PrimaryValue(BattleRuntime runtime, PendingAbility pending)
        => Math.Max(0, BaseEffectAmount(runtime, pending, pending.Ability.Definition.Effects.FirstOrDefault(), true) ?? 0);

    // 卡面读取当下的施加量；伤害在敌方护甲扣减前展示，治疗不按缺失生命截断。
    internal static int? PreviewEffectAmount(BattleRuntime runtime, CardBattleState card,
        BattleAbilityState ability, EffectDefinition effect, bool hasOpponent)
    {
        if (!hasOpponent && (effect is MaxHealthPercentDamageEffectDefinition
            || effect is HealEffectDefinition { UnlessEnemyTag: not null }
            || effect is ApplyPrimaryEffectPercentStatusEffectDefinition
                && ability.Definition.Effects.FirstOrDefault() is MaxHealthPercentDamageEffectDefinition)) return null;
        var pending = new PendingAbility(card, ability, false, false, runtime.Tick)
            { DamageMultiplier = ElementSequenceResolver.PreviewMultiplier(runtime, card, ability) };
        pending = pending with { PrimaryValue = PrimaryValue(runtime, pending) };
        var amount = BaseEffectAmount(runtime, pending, effect);
        var usesBerserk = effect is HeroManaDamageEffectDefinition or SkillCountDamageEffectDefinition or DamageEffectDefinition or AttributeDamageEffectDefinition
            or DestroyAlliedCardsAndDamageEffectDefinition or SourceHeroHealthScaledAttributeDamageEffectDefinition
            or SourceHeroArmorDamageEffectDefinition or SourceHeroLevelScaledDamageEffectDefinition or MaxHealthPercentDamageEffectDefinition
            or ApplyStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplyPrimaryEffectPercentStatusEffectDefinition;
        return amount is { } value && IsDamageEffect(effect) ? ActivationDamageAmount(pending, value)
            : amount is { } statusValue && usesBerserk ? BerserkAmount(pending, statusValue) : amount;
    }

    private static int? BaseEffectAmount(BattleRuntime runtime, PendingAbility pending,
        EffectDefinition? effect, bool primaryValue = false)
    {
        int Value(StringName key) => GetEffectiveCombatAttribute(runtime, pending.Source, key);
        var own = runtime.GetHero(pending.Source.Side);
        var target = runtime.GetHero(pending.Ability.Definition.Target == AbilityTarget.EnemyHero ? Opposite(pending.Source.Side) : pending.Source.Side);
        return effect switch
        {
            ReduceHeroStatusEffectDefinition value => -Math.Min(value.Amount, value.Status == BattleStatus.Burn ? target.Burn : target.Poison),
            AttributeDamageEffectDefinition value => AttributeDamageAmount(runtime, pending, value),
            SourceHeroHealthScaledAttributeDamageEffectDefinition value => primaryValue ? Value(value.AttributeKey)
                : checked((int)((long)Value(value.AttributeKey) * own.Health / own.MaxHealth)),
            DestroyAlliedCardsAndDamageEffectDefinition value => Value(value.DamageAttributeKey),
            GainSourceHeroArmorFromAttributeEffectDefinition value => Value(value.AttributeKey),
            ApplyAttributeStatusEffectDefinition value => Value(value.AttributeKey)
                + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            ApplySourceAttributePercentStatusEffectDefinition value => checked((int)((long)Value(value.AttributeKey) * value.Percent / 100))
                + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            ApplyStatusEffectDefinition value => value.Amount + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            HeroManaDamageEffectDefinition value => checked(own.MaxMana + own.ManaRegen * value.RegenMultiplier + Value(GameAttributeKeys.AttackDamage)),
            IncreaseHeroMaxManaEffectDefinition value => value.Amount,
            ManaRegenAuraEffectDefinition value => value.Amount,
            RestoreEventManaSpentEffectDefinition => pending.EventManaSpent,
            SkillCountDamageEffectDefinition value => checked(runtime.Skills.Count(skill => skill.Side == pending.Source.Side) * value.Multiplier + Value(GameAttributeKeys.AttackDamage)),
            SkillCountMaxManaEffectDefinition value => checked(runtime.Skills.Count(skill => skill.Side == pending.Source.Side) * value.Multiplier),
            SkillCountManaRegenAuraEffectDefinition value => checked(runtime.Skills.Count(skill => skill.Side == pending.Source.Side) * value.Multiplier),
            DamageEffectDefinition value => value.Amount,
            HealEffectDefinition value => CanHeal(runtime, pending.Source.Side, value)
                ? HealingAmount(runtime, pending.Source, value) : 0,
            HealMaxHealthPercentEffectDefinition value => checked((int)((long)target.MaxHealth * value.Percent / 100)
                + Value(GameAttributeKeys.HealingBonus)),
            ArmorEffectDefinition value => value.Amount,
            GainSourceHeroArmorEffectDefinition value => value.Amount,
            GainSourceHeroLevelScaledArmorEffectDefinition value => checked(own.Level * value.Multiplier),
            SourceHeroLevelScaledDamageEffectDefinition value => checked(own.Level * value.Multiplier),
            GainArmorEqualToManaSpentEffectDefinition => own.ManaSpent,
            SourceHeroArmorDamageEffectDefinition value => checked((own.Armor
                + (value.BonusAttributeKey is { IsEmpty: false } key ? Value(key) : 0)) * value.Multiplier),
            MaxHealthPercentDamageEffectDefinition value => checked((int)((long)target.MaxHealth * value.Percent / 100)),
            RestoreManaEffectDefinition value => value.Amount,
            ChargeSourceCardEffectDefinition value => value.AmountTicks,
            ChargeCardEffectDefinition value => value.AmountTicks,
            ApplyPrimaryEffectPercentStatusEffectDefinition value when !primaryValue =>
                checked(value.CalculateAmount(pending.Ability.Definition.Effects.FirstOrDefault(), pending.PrimaryValue)
                    + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0)),
            IncreaseAlliedCardAttributeAuraEffectDefinition value => checked(value.Amount + own.Level * value.HeroLevelMultiplier
                + (pending.Source is CardBattleState auraSource ? auraSource.FrozenValue : 0) * value.SourceValueMultiplier),
            _ => null,
        };
    }

    // 系数按己方存活战场匹配元素的卡牌数计算，治疗加成在乘积外累加。
    private static int HealingAmount(BattleRuntime runtime, IBattleAbilitySource source, HealEffectDefinition effect) => checked(
        effect.Amount * (effect.PerAlliedElementKey is { } element
            ? runtime.Cards.Count(card => card.Side == source.Side && !card.Destroyed && !card.IsOnBench && card.ElementKeys.Contains(element)) : 1)
        + GetEffectiveCombatAttribute(runtime, source, GameAttributeKeys.HealingBonus));

    private static bool CanHeal(BattleRuntime runtime, SideId side, HealEffectDefinition effect) =>
        effect.UnlessEnemyTag is not { } tag || !runtime.Cards.Any(card => card.Side != side
            && !card.Destroyed && !card.IsOnBench && HasEffectiveTag(runtime, card, tag));

    private static int AttributeDamageAmount(BattleRuntime runtime, PendingAbility pending, AttributeDamageEffectDefinition effect)
    {
        var multiplier = pending.Source is CardBattleState { ActiveActivationCount: 0 }
            ? effect.FirstActivationMultiplier : 1;
        var amount = checked((int)((long)GetEffectiveCombatAttribute(runtime, pending.Source, effect.AttributeKey) * effect.Percent / 100));
        return checked(amount * multiplier);
    }

    private static int BerserkAmount(PendingAbility pending, int amount) =>
        pending.Source is CardBattleState { IsBerserk: true }
            && pending.Ability.Definition.Activation == AbilityActivation.Active
                ? checked((int)((long)amount * 120 / 100)) : amount;

    private static int ActivationDamageAmount(PendingAbility pending, int amount) =>
        checked((int)(BerserkAmount(pending, amount) * pending.DamageMultiplier));

    internal static bool IsDamageEffect(EffectDefinition effect) => effect is HeroManaDamageEffectDefinition or SkillCountDamageEffectDefinition or DamageEffectDefinition or AttributeDamageEffectDefinition
        or DestroyAlliedCardsAndDamageEffectDefinition or SourceHeroHealthScaledAttributeDamageEffectDefinition
        or SourceHeroArmorDamageEffectDefinition or SourceHeroLevelScaledDamageEffectDefinition or MaxHealthPercentDamageEffectDefinition;

    // 查询来源的基础属性与当前有效光环贡献，不提交属性写入。
    internal static int GetEffectiveCombatAttribute(BattleRuntime runtime, IBattleAbilitySource source, StringName key)
    {
        var value = source.GetCombatAttribute(key);
        if (source is CardBattleState target)
            foreach (var aura in AlliedAttributeAuras(runtime, target))
                if (aura.AttributeKey == key) value = checked(value + aura.Amount);
        if (source is CardBattleState { Destroyed: false, IsOnBench: false } cooldownSource
            && cooldownSource.SupportsCombatAttribute(key))
        {
            foreach (var ability in cooldownSource.Abilities)
            foreach (var effect in ability.Definition.Effects)
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is IncreaseSourceAttributePerCooldownSecondEffectDefinition increase
                    && increase.AttributeKey == key)
                {
                    var duration = cooldownSource.Abilities
                        .Where(active => active.Definition.Activation == AbilityActivation.Active)
                        .Select(active => BattleCooldownRules.DurationUnits(active.Definition.CooldownTicks,
                            cooldownSource.CooldownBonusTicks, cooldownSource.EffectiveCooldownMultiplier))
                        .DefaultIfEmpty(0m).Max();
                    value = checked(value + (int)decimal.Floor(duration * increase.AmountPerSecond / 20m));
                }
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition multiplier
                || multiplier.AttributeKey != key)
            {
                continue;
            }
            foreach (var card in runtime.Cards)
            {
                if (card.Destroyed && ContainsAnyTag(card, multiplier.RequiredAnyTags))
                    value = checked(value * multiplier.Multiplier);
            }
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition increase
                || increase.AttributeKey != key)
            {
                continue;
            }
            foreach (var card in runtime.Cards)
            {
                if (card.Side != source.Side && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, increase.RequiredTag))
                {
                    value = checked(value + increase.Amount);
                }
            }
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition increase
                || increase.AttributeKey != key)
                continue;
            foreach (var card in runtime.Cards)
                if (card.Side == source.Side && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, increase.RequiredTag)
                    && (!increase.AdjacentOnly || source is CardBattleState { Destroyed: false, IsOnBench: false } origin
                        && card.EntityId != origin.EntityId
                        && (card.BoardStart + card.OccupiedSlots == origin.BoardStart
                            || origin.BoardStart + origin.OccupiedSlots == card.BoardStart)))
                    value = checked(value + increase.Amount);
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
            if (ability.Definition.Activation == AbilityActivation.PassiveAura
                && effect is MultiplySourceAttributeWhileEnemyHasArmorEffectDefinition multiplier
                && multiplier.AttributeKey == key && runtime.GetHero(Opposite(source.Side)).Armor > 0)
                value = checked(value * multiplier.Multiplier);
        if (source is CardBattleState { Destroyed: false, IsOnBench: false })
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is MultiplySourceAttributeWhileEnemyHasTagEffectDefinition tagMultiplier
                    && tagMultiplier.AttributeKey == key
                    && runtime.Cards.Any(card => card.Side != source.Side && !card.Destroyed && !card.IsOnBench
                        && HasEffectiveTag(runtime, card, tagMultiplier.RequiredTag)))
                    value = checked(value * tagMultiplier.Multiplier);
        if (source is CardBattleState { Destroyed: false, IsOnBench: false } stateSource)
            foreach (var ability in stateSource.Abilities)
            foreach (var effect in ability.Definition.Effects)
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is MultiplySourceAttributeWhileInStateEffectDefinition stateMultiplier
                    && stateMultiplier.AttributeKey == key
                    && (stateMultiplier.StateKey == GameAttributeKeys.Berserk ? stateSource.IsBerserk : stateSource.IsFlying))
                    value = checked(value * stateMultiplier.Multiplier);
        if (source is CardBattleState { Destroyed: false, IsOnBench: false } receiver
            && receiver.SupportsCombatAttribute(key))
        {
            foreach (var auraSource in runtime.AbilitySources)
            {
                if (auraSource.Destroyed || auraSource.IsOnBench || auraSource.Side != receiver.Side) continue;
                foreach (var ability in auraSource.Abilities)
                foreach (var effect in ability.Definition.Effects)
                    if (ability.Definition.Activation == AbilityActivation.PassiveAura
                        && effect is MultiplyAlliedElementCardAttributeAuraEffectDefinition aura
                        && aura.AttributeKey == key && receiver.ElementKeys.Contains(aura.ElementKey)
                        && (aura.RequiredEnemyAnyTags is null || runtime.Cards.Any(card =>
                            card.Side != receiver.Side && !card.Destroyed && !card.IsOnBench
                            && ContainsAnyEffectiveTag(runtime, card, aura.RequiredEnemyAnyTags))))
                        value = checked(value * aura.Multiplier);
            }
        }
        return value;
    }

    // 贡献只读取光环定义，不依赖其他卡牌最终值；来源失效后自然停止计入。
    internal static System.Collections.Generic.IEnumerable<IncreaseAlliedCardAttributeAuraEffectDefinition> AlliedAttributeAuras(
        BattleRuntime runtime, CardBattleState target)
    {
        if (target.Destroyed || target.IsOnBench) yield break;
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Destroyed || source.IsOnBench || source.Side != target.Side) continue;
            foreach (var ability in source.Abilities)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                foreach (var effect in ability.Definition.Effects)
                    if (effect is IncreaseAlliedCardAttributeAuraEffectDefinition aura
                        && target.SupportsCombatAttribute(aura.AttributeKey)
                        && (!aura.AttackCardsOnly || target.IsAttackCard)
                        && (aura.RequiredTag is not { IsEmpty: false } requiredTag || HasEffectiveTag(runtime, target, requiredTag))
                        && (aura.ExtremeSide is not { } side || !runtime.Cards.Any(candidate =>
                            candidate.Side == target.Side && !candidate.Destroyed && !candidate.IsOnBench
                            && candidate.SupportsCombatAttribute(aura.AttributeKey)
                            && (side == AdjacentCardSide.Left ? candidate.BoardStart < target.BoardStart
                                : candidate.BoardStart > target.BoardStart))))
                        yield return aura with
                        {
                            Amount = checked(aura.Amount + checked(runtime.GetHero(source.Side).Level * aura.HeroLevelMultiplier)
                                + checked((source is CardBattleState cardSource ? cardSource.FrozenValue : 0) * aura.SourceValueMultiplier)),
                            HeroLevelMultiplier = 0,
                            SourceValueMultiplier = 0,
                        };
            }
        }
    }

    // 为事件与回放提供强类型能力来源标识。
    internal static AbilitySourceKind GetAbilitySourceKind(IBattleAbilitySource source) => source switch
    {
        SkillBattleState => AbilitySourceKind.Skill,
        CardSetBattleState => AbilitySourceKind.CardSet,
        _ => AbilitySourceKind.Card,
    };

    private static DamageSourceKind GetDamageSourceKind(IBattleAbilitySource source) => source switch
    {
        SkillBattleState => DamageSourceKind.Skill,
        CardSetBattleState => DamageSourceKind.CardSet,
        _ => DamageSourceKind.Card,
    };

    private static void DestroyRandomEnemyCard(
        BattleRuntime runtime,
        IBattleAbilitySource source,
        DestroyRandomEnemyCardEffectDefinition effect, bool suppressEchoes)
    {
        var candidates = new System.Collections.Generic.List<CardBattleState>();
        foreach (var card in runtime.Cards)
        {
            if (card.Side == source.Side || card.Destroyed || card.IsOnBench
                || !effect.AllowedSizes.Contains((CardSize)card.OccupiedSlots)
                || !ContainsAnyEffectiveTag(runtime, card, effect.RequiredAnyTags))
            {
                continue;
            }
            candidates.Add(card);
        }
        if (candidates.Count == 0) return;
        DestroyCard(runtime, candidates[runtime.NextRandomIndex(candidates.Count)], source.EntityId, effect.Permanent, suppressEchoes);
    }

    // 以新的 Runtime 状态替换目标；旧队列来源失效，对局实例不受影响。
    private static void TransformRandomEnemyCard(BattleRuntime runtime, IBattleAbilitySource source,
        TransformRandomEnemyCardEffectDefinition effect)
    {
        var definition = effect.Replacement;
        var identity = definition.Attributes.Identity;
        var candidates = runtime.Cards.Where(card => card.Side != source.Side && !card.Destroyed && !card.IsOnBench
            && card.OccupiedSlots == identity.OccupiedSlots && definition.SupportsLevel(card.Level)).ToArray();
        if (candidates.Length == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Length)];
        var level = definition.GetLevel(target.Level);
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        if (level is not null)
            foreach (var entry in level.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
        var replacement = new CardBattleSetup(target.EntityId, target.BoardStart,
            values.GetFinalValue(GameAttributeKeys.AttackDamage), values.GetFinalValue(GameAttributeKeys.CooldownTicks),
            level?.Abilities ?? definition.Abilities, UseLegacyAttack: false, Tags: definition.Tags,
            Multicast: values.GetFinalValue(GameAttributeKeys.Multicast), OccupiedSlots: identity.OccupiedSlots,
            ElementKeys: identity.ElementKeys, ArmorAmount: values.GetFinalValue(GameAttributeKeys.Armor))
            { UpgradeLevels = BattleCardLevels.Capture(definition), Identity = identity, Level = target.Level, CombatValues = values.SnapshotFinalValues(), Quests = definition.Quests };
        var transformed = new CardBattleState(replacement, target.Side)
            { TransformedIdentity = identity, SummonedCard = target.SummonedCard };
        foreach (var entry in replacement.CombatValues) transformed.CombatAttributes[entry.Key] = entry.Value;
        runtime.Cards[runtime.Cards.IndexOf(target)] = transformed;
        runtime.Queue.CancelSource(target);
        runtime.ScheduledMulticasts.RemoveAll(item => ReferenceEquals(item.Ability.Source, target));
        runtime.Events.Add(new CardTransformedEvent(runtime.Tick, source.EntityId, target.EntityId, identity.Key));
    }

    // 原子替换来源占格；不触发摧毁，取消旧发动队列，新卡从完整冷却开始。
    private static void TransformSourceIntoRandomCards(BattleRuntime runtime, CardBattleState source,
        TransformSourceIntoRandomCardsEffectDefinition effect, bool suppressEchoes)
    {
        if (source.Destroyed || source.IsOnBench || !runtime.Cards.Contains(source)) return;
        var candidates = effect.Replacements.Where(card => card.SupportsLevel(source.Level))
            .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToList();
        if (candidates.Count < effect.Count
            || (long)candidates[0].Attributes.Identity.OccupiedSlots * effect.Count != source.OccupiedSlots) return;
        var replacements = new List<CardBattleState>();
        var start = source.BoardStart;
        for (var index = 0; index < effect.Count; index++)
        {
            var selected = runtime.NextRandomIndex(candidates.Count);
            var definition = candidates[selected]; candidates.RemoveAt(selected);
            var identity = definition.Attributes.Identity;
            var level = definition.GetLevel(source.Level);
            var values = definition.Attributes.BaseCombat.CreateMutableCopy();
            if (level is not null)
                foreach (var entry in level.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
            var id = index == 0 ? source.EntityId : runtime.NewSummonedCardId(source.EntityId);
            var setup = new CardBattleSetup(id, start, values.GetFinalValue(GameAttributeKeys.AttackDamage),
                values.GetFinalValue(GameAttributeKeys.CooldownTicks), level?.Abilities ?? definition.Abilities,
                UseLegacyAttack: false, Tags: definition.Tags, Multicast: values.GetFinalValue(GameAttributeKeys.Multicast),
                OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys,
                ArmorAmount: values.GetFinalValue(GameAttributeKeys.Armor))
                { UpgradeLevels = BattleCardLevels.Capture(definition), Identity = identity, Level = source.Level, CombatValues = values.SnapshotFinalValues(), Quests = definition.Quests };
            var replacement = new CardBattleState(setup, source.Side)
            {
                TransformedIdentity = index == 0 ? identity : null,
                SummonedCard = index == 0 ? source.SummonedCard : new SummonedCardSnapshot(identity, start,
                    Array.AsReadOnly(definition.Tags.ToArray()), Array.AsReadOnly(setup.Abilities!.ToArray()), setup.CombatValues),
            };
            foreach (var entry in setup.CombatValues) replacement.CombatAttributes[entry.Key] = entry.Value;
            replacements.Add(replacement);
            start += identity.OccupiedSlots;
        }
        runtime.Queue.CancelSource(source);
        runtime.ScheduledMulticasts.RemoveAll(item => ReferenceEquals(item.Ability.Source, source));
        runtime.Cards[runtime.Cards.IndexOf(source)] = replacements[0];
        runtime.Events.Add(new CardTransformedEvent(runtime.Tick, source.EntityId, source.EntityId,
            replacements[0].Identity!.Key));
        foreach (var replacement in replacements.Skip(1))
        {
            runtime.AddSummonedCard(replacement);
            runtime.Events.Add(new CardSummonedEvent(runtime.Tick, source.EntityId, replacement.EntityId, source.Side,
                replacement.Identity!.Key, source.Level, replacement.BoardStart));
            NotifySummoned(runtime, replacement, suppressEchoes);
        }
    }

    // 先验证等级、边界和占位，失败时不消耗随机数。
    private static bool CanSummonAdjacentCard(BattleRuntime runtime, CardBattleState source,
        CardDefinition definition, AdjacentCardSide side)
    {
        var size = definition.Attributes.Identity.OccupiedSlots;
        var start = side == AdjacentCardSide.Left ? source.BoardStart - size : source.BoardStart + source.OccupiedSlots;
        return definition.SupportsLevel(source.Level) && start >= 0 && start + size <= runtime.GetBattlefieldCapacity(source.Side)
            && !runtime.Cards.Any(card => card.Side == source.Side && !card.IsOnBench && !card.Destroyed
                && card.BoardStart < start + size && start < card.BoardStart + card.OccupiedSlots);
    }

    private static void SummonAdjacentCard(BattleRuntime runtime, CardBattleState source,
        SummonAdjacentCardEffectDefinition effect, bool suppressEchoes)
    {
        var definition = effect.Card;
        var identity = definition.Attributes.Identity;
        var size = identity.OccupiedSlots;
        var start = effect.Side == AdjacentCardSide.Left ? source.BoardStart - size : source.BoardStart + source.OccupiedSlots;
        if (!CanSummonAdjacentCard(runtime, source, definition, effect.Side)) return;
        SummonCard(runtime, source, definition, source.Level, start, suppressEchoes);
    }

    // 校验完整占格并创建本场召唤物，卡牌与技能来源共用。
    private static CardBattleState? SummonCard(BattleRuntime runtime, IBattleAbilitySource source, CardDefinition definition,
        int cardLevel, int start, bool suppressEchoes = false)
    {
        var identity = definition.Attributes.Identity;
        var size = identity.OccupiedSlots;
        if (!definition.SupportsLevel(cardLevel) || start < 0 || start + size > runtime.GetBattlefieldCapacity(source.Side)
            || runtime.Cards.Any(card => card.Side == source.Side && !card.IsOnBench && !card.Destroyed
                && card.BoardStart < start + size && start < card.BoardStart + card.OccupiedSlots)) return null;
        var level = definition.GetLevel(cardLevel);
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        if (level is not null)
            foreach (var entry in level.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
        var id = runtime.NewSummonedCardId(source.EntityId);
        var setup = new CardBattleSetup(id, start, values.GetFinalValue(GameAttributeKeys.AttackDamage),
            values.GetFinalValue(GameAttributeKeys.CooldownTicks), level?.Abilities ?? definition.Abilities,
            UseLegacyAttack: false, Tags: definition.Tags, Multicast: values.GetFinalValue(GameAttributeKeys.Multicast),
            OccupiedSlots: size, ElementKeys: identity.ElementKeys, ArmorAmount: values.GetFinalValue(GameAttributeKeys.Armor))
            { UpgradeLevels = BattleCardLevels.Capture(definition), Identity = identity, Level = cardLevel, CombatValues = values.SnapshotFinalValues(), Quests = definition.Quests };
        var summoned = new CardBattleState(setup, source.Side)
        {
            SummonedCard = new SummonedCardSnapshot(identity, start, Array.AsReadOnly(definition.Tags.ToArray()),
                Array.AsReadOnly(setup.Abilities!.ToArray()), setup.CombatValues),
        };
        foreach (var entry in setup.CombatValues) summoned.CombatAttributes[entry.Key] = entry.Value;
        runtime.AddSummonedCard(summoned);
        runtime.Events.Add(new CardSummonedEvent(runtime.Tick, source.EntityId, id, source.Side, identity.Key, cardLevel, start));
        NotifySummoned(runtime, summoned, suppressEchoes);
        return summoned;
    }

    // 每张实际召唤物仅发布一次回响；回响产生的召唤遵循不连锁约定。
    private static void NotifySummoned(BattleRuntime runtime, CardBattleState summoned, bool suppressEchoes)
    {
        if (suppressEchoes) return;
        foreach (var source in runtime.AbilitySources.Where(source => source.Side == summoned.Side && !source.Destroyed && !source.IsOnBench))
        foreach (var ability in source.Abilities)
            if (ability.Definition.Activation == AbilityActivation.EchoOnAlliedCardSummoned
                && (ability.Definition.TriggerCardElement is not { } element || summoned.ElementKeys.Contains(element))
                && (ability.Definition.TriggerCardTag is not { } tag || HasEffectiveTag(runtime, summoned, tag))
                && (ability.Definition.TriggerCardSize is not { } size || summoned.OccupiedSlots == (int)size))
                CombatSimulator.Enqueue(runtime, source, ability, true, eventCard: summoned);
    }

    private static void ChargeRandomOtherAlliedElementCard(
        BattleRuntime runtime,
        PendingAbility pending,
        CardBattleState source,
        ChargeRandomOtherAlliedElementCardEffectDefinition effect)
    {
        var candidates = new System.Collections.Generic.List<CardBattleState>();
        foreach (var card in runtime.Cards)
        {
            if (card.Side != source.Side || card.EntityId == source.EntityId || card.Destroyed || card.IsOnBench
                || effect.ExcludeEventCard && card.EntityId == pending.EventCard?.EntityId
                || !card.ElementKeys.Contains(effect.ElementKey)
                || !card.Abilities.Any(ability =>
                    ability.Definition.Activation == AbilityActivation.Active
                    && ability.RemainingCooldownUnits > 0))
            {
                continue;
            }
            candidates.Add(card);
        }
        if (candidates.Count == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Count)];
        ChargeCard(runtime, source, target, effect.AmountTicks, pending.IsEcho || pending.SuppressEchoes);
    }

    private static CardBattleState? GetTargetCard(BattleRuntime runtime, PendingAbility pending)
    {
        if (pending.Source is not CardBattleState source)
            throw new InvalidOperationException("Card target requires a card source.");
        if (pending.Ability.Definition.Target == AbilityTarget.SelfCard) return source;
        if (pending.Ability.Definition.Target == AbilityTarget.RightmostAlliedCard)
            return runtime.Cards.Where(card => card.Side == source.Side && !card.Destroyed && !card.IsOnBench)
                .OrderByDescending(card => card.BoardStart).FirstOrDefault();
        return runtime.Cards.FirstOrDefault(card => card.Side == source.Side && !card.Destroyed && !card.IsOnBench
            && (pending.Ability.Definition.TargetCardTag is not { } tag || HasEffectiveTag(runtime, card, tag))
            && (pending.Ability.Definition.Target == AbilityTarget.LeftAdjacentAlliedCard
                ? card.BoardStart + card.OccupiedSlots == source.BoardStart
                : card.BoardStart == source.BoardStart + source.OccupiedSlots));
    }

    // 共用充能结算，降至零时只对原本尚在冷却的主动能力排队。
    private static void ChargeCard(BattleRuntime runtime, IBattleAbilitySource source, CardBattleState target, int amountTicks, bool suppressEchoes)
    {
        foreach (var ability in target.Abilities)
        {
            if (ability.Definition.Activation != AbilityActivation.Active
                || ability.RemainingCooldownUnits == 0)
            {
                continue;
            }
            ability.RemainingCooldownUnits = Math.Max(
                0,
                ability.RemainingCooldownUnits - amountTicks * 2);
            if (ability.RemainingCooldownUnits == 0)
                CombatSimulator.Enqueue(runtime, target, ability, false, suppressEchoes: suppressEchoes);
        }
        runtime.Events.Add(new CardChargedEvent(
            runtime.Tick,
            source.EntityId,
            target.EntityId,
            amountTicks));
    }

    private static void SetCardState(BattleRuntime runtime, PendingAbility pending,
        CardBattleState target, StringName stateKey, bool enabled)
    {
        var previous = stateKey == GameAttributeKeys.Flying ? target.IsFlying : target.IsBerserk;
        if (stateKey == GameAttributeKeys.Flying) target.IsFlying = enabled;
        else target.IsBerserk = enabled;
        if (previous == enabled) return;
        runtime.Events.Add(new CardStateChangedEvent(runtime.Tick, target.EntityId, stateKey, enabled));
        if (enabled && !pending.IsEcho)
            CombatSimulator.EnqueueStateEntryEchoes(runtime, target, stateKey);
    }

    private static bool ContainsAnyTag(CardBattleState card, System.Collections.Generic.IReadOnlyList<StringName> tags)
    {
        foreach (var tag in tags)
            if (card.Destroyed ? card.DestroyedTags.Contains(tag) : card.Tags.Contains(tag)) return true;
        return false;
    }

    private static bool ContainsAnyEffectiveTag(
        BattleRuntime runtime,
        CardBattleState card,
        System.Collections.Generic.IReadOnlyList<StringName> tags)
    {
        foreach (var tag in tags)
            if (HasEffectiveTag(runtime, card, tag)) return true;
        return false;
    }

    internal static bool HasEffectiveTag(BattleRuntime runtime, CardBattleState card, StringName tag)
    {
        if (card.Destroyed) return card.DestroyedTags.Contains(tag);
        if (card.Tags.Contains(tag)) return true;
        if (card.IsOnBench) return false;
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                if (source.Side == card.Side && effect is GrantTagToAlliedElementCardsEffectDefinition allied
                    && allied.Tag == tag && card.ElementKeys.Contains(allied.ElementKey)) return true;
                if (source.Side != card.Side && effect is GrantTagToEnemySizeCardsEffectDefinition enemy
                    && enemy.Tag == tag && (int)enemy.Size == card.OccupiedSlots) return true;
            }
        }
        return false;
    }

    // 标签仅在查询与回放副本中合并，不改写卡牌原身份或永久标签。
    internal static StringName[] EffectiveTags(BattleRuntime runtime, CardBattleState card) => card.Destroyed
        ? card.DestroyedTags.ToArray()
        : card.Tags.Concat(runtime.AbilitySources.Where(source => !source.Destroyed && !source.IsOnBench)
            .SelectMany(source => source.Abilities).Where(ability => ability.Definition.Activation == AbilityActivation.PassiveAura)
            .SelectMany(ability => ability.Definition.Effects).Select(effect => effect switch
            {
                GrantTagToAlliedElementCardsEffectDefinition allied => allied.Tag,
                GrantTagToEnemySizeCardsEffectDefinition enemy => enemy.Tag,
                _ => null,
            }).Where(tag => tag is not null && HasEffectiveTag(runtime, card, tag)).Cast<StringName>()).Distinct().ToArray();

    // 先筛选空闲原位置，修复只操作已有Runtime，不重建身份或重放开战能力。
    private static void RepairRandomDestroyedAlliedCard(BattleRuntime runtime, PendingAbility pending,
        RepairRandomDestroyedAlliedCardEffectDefinition effect)
    {
        var candidates = runtime.Cards.Where(card => card.Side == pending.Source.Side && card.Destroyed && !card.IsOnBench
            && card.DestroyedTags.Contains(effect.RequiredTag)
            && !runtime.PermanentChanges.Any(change => change.CardId == card.EntityId && change.ChangeType == "Destroy")
            && !runtime.Cards.Any(other => other.Side == card.Side && !other.Destroyed && !other.IsOnBench
                && other.BoardStart < card.BoardStart + card.OccupiedSlots
                && card.BoardStart < other.BoardStart + other.OccupiedSlots)).ToArray();
        if (candidates.Length == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Length)];
        target.Destroyed = false;
        target.DestroyedTags.Clear();
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        foreach (var ability in target.Abilities)
            if (ability.Definition.Activation == AbilityActivation.Active)
                ability.RemainingCooldownUnits = BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks,
                    target.CooldownBonusTicks, target.EffectiveCooldownMultiplier);
        runtime.Events.Add(new CardRepairedEvent(runtime.Tick, pending.Source.EntityId, target.EntityId,
            target.Side, target.BoardStart, target.OccupiedSlots));
    }

    internal static void DestroyCard(BattleRuntime runtime, CardBattleState target, EntityId sourceId, bool permanent, bool suppressEchoes)
    {
        if (target.Destroyed) return;
        foreach (var tag in EffectiveTags(runtime, target)) target.DestroyedTags.Add(tag);
        target.Destroyed = true;
        runtime.Queue.CancelSource(target);
        runtime.ScheduledMulticasts.RemoveAll(item => ReferenceEquals(item.Ability.Source, target));
        runtime.Events.Add(new CardDestroyedEvent(runtime.Tick, target.EntityId, target.Side, sourceId));
        foreach (var ability in target.Abilities.Where(ability => ability.Definition.Activation == AbilityActivation.EchoOnSourceDestroyed))
            CombatSimulator.Enqueue(runtime, target, ability, true, allowDestroyedSource: true);
        if (!suppressEchoes && !target.IsOnBench)
            foreach (var source in runtime.AbilitySources.Where(source => source.Side == target.Side && !source.Destroyed && !source.IsOnBench))
            foreach (var ability in source.Abilities)
                if (ability.Definition.Activation == AbilityActivation.EchoOnAlliedCardDestroyed
                    && (ability.Definition.TriggerCardTag is not { } tag || target.DestroyedTags.Contains(tag))
                    && (ability.Definition.TriggerCardSize is not { } size || target.OccupiedSlots == (int)size)
                    && (ability.Definition.TriggerCardElement is not { } element || target.ElementKeys.Contains(element))
                    && (ability.Definition.ExcludedTriggerCardElement is not { } excluded || !target.ElementKeys.Contains(excluded)))
                    CombatSimulator.Enqueue(runtime, source, ability, true, eventCard: target);
        if (permanent && target.SummonedCard is null) runtime.PermanentChanges.Add(new PermanentChange(target.EntityId, "Destroy"));
    }

    // 以完整冷却差值调整剩余时长，避免到达一秒下限后继续扣减进度。
    private static void AdjustSourceCooldown(IBattleAbilitySource source, int amountTicks)
    {
        var oldBonus = source.CooldownBonusTicks;
        source.CooldownBonusTicks = checked(oldBonus + amountTicks);
        var multiplier = source is CardBattleState card ? card.EffectiveCooldownMultiplier : 1m;
        foreach (var ability in source.Abilities)
            if (ability.Definition.Activation == AbilityActivation.Active)
                ability.RemainingCooldownUnits = Math.Max(0m, ability.RemainingCooldownUnits
                    + BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, source.CooldownBonusTicks, multiplier)
                    - BattleCooldownRules.DurationUnits(ability.Definition.CooldownTicks, oldBonus, multiplier));
    }

    // 统一扣减护甲与生命，并记录伤害事件及对应冻结状态。
    internal static bool ApplyDamage(
        BattleRuntime runtime,
        EntityId source,
        SideId targetSide,
        HeroBattleState target,
        int amount,
        bool bypassArmor,
        DamageSourceKind sourceKind = DamageSourceKind.Card,
        BattleStatus? statusOrigin = null)
    {
        if (target.StealthDurationTicks > 0 && sourceKind is DamageSourceKind.Card or DamageSourceKind.Skill or DamageSourceKind.CardSet)
        {
            runtime.Events.Add(new AttackBlockedEvent(runtime.Tick, source, targetSide));
            BattleStateRecorder.CaptureState(runtime);
            return false;
        }
        var previousHealth = target.Health;
        var absorbed = bypassArmor ? 0 : Math.Min(target.Armor, amount);
        target.Armor -= absorbed;
        var healthDamage = amount - absorbed;
        target.Health = Math.Max(0, target.Health - healthDamage);
        runtime.Events.Add(new DamageDealtEvent(
            runtime.Tick,
            source,
            targetSide,
            amount,
            absorbed,
            healthDamage,
            target.Health,
            sourceKind) { StatusOrigin = statusOrigin });
        BattleStateRecorder.CaptureState(runtime);
        CombatSimulator.ResolveHeroHealthTriggers(runtime, targetSide, previousHealth);
        return true;
    }

    private static SideId Opposite(SideId side) => side == SideId.Player ? SideId.Opponent : SideId.Player;
}
