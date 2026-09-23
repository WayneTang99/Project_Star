using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

/// <summary>Runs deterministic combat, including abilities, statuses, eclipse, and extinction.</summary>
public sealed class CombatSimulator
{
    public BattleResult Simulate(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var runtime = new BattleRuntime(setup);
        runtime.Events.Add(new BattleStartedEvent(runtime.Tick));
        EnqueueBattleStartAbilities(runtime);
        ResolveQueue(runtime);
        var openingResult = TryCreateDefeatResult(runtime);
        if (openingResult is not null) return openingResult;
        while (runtime.Tick.Value < setup.Timeout.Value)
        {
            runtime.Tick += new BattleTick(1);
            ApplyEclipse(runtime, setup);
            AdvanceCooldowns(runtime);
            EnqueueReadyActives(runtime);
            SettleHeroStatuses(runtime);
            ResolveQueue(runtime);
            var result = TryCreateDefeatResult(runtime);
            if (result is not null) return result;
        }

        runtime.PlayerHero.Health = 0;
        runtime.OpponentHero.Health = 0;
        return CreateResult(runtime, BattleOutcome.PlayerVictory, BattleEndReason.Extinction);
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

    private static void AdvanceCooldowns(BattleRuntime runtime)
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

    private static void Enqueue(
        BattleRuntime runtime,
        IBattleAbilitySource card,
        BattleAbilityState ability,
        bool echo,
        bool multicast = false)
    {
        runtime.Queue.Enqueue(new PendingAbility(card, ability, echo, multicast, runtime.Tick));
        runtime.Events.Add(new AbilityQueuedEvent(
            runtime.Tick,
            card.EntityId,
            card.Side,
            card is SkillBattleState ? AbilitySourceKind.Skill : AbilitySourceKind.Card));
    }

    private static void ResolveQueue(BattleRuntime runtime)
    {
        while (runtime.Queue.Count > 0)
        {
            var pending = runtime.Queue.Dequeue();
            if (pending.Source.Destroyed) continue;
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
                pending.Ability.RemainingCooldownUnits = (definition.CooldownTicks + pending.Source.CooldownBonusTicks) * 2;
            runtime.Events.Add(new AbilityActivatedEvent(
                runtime.Tick,
                pending.Source.EntityId,
                pending.Source.Side,
                pending.IsEcho,
                pending.Source is SkillBattleState ? AbilitySourceKind.Skill : AbilitySourceKind.Card));
            if (!pending.IsEcho && !pending.IsMulticast && pending.Source is CardBattleState cardSource)
                for (var repeat = 0; repeat < GetEffectiveMulticast(runtime, cardSource); repeat++)
                    Enqueue(runtime, cardSource, pending.Ability, false, true);
            foreach (var effect in definition.Effects) ApplyEffect(runtime, pending, effect);
            if (!pending.IsEcho) pending.Source.ActivationCount++;
            if (!pending.IsEcho) EnqueueEchoes(runtime, pending);
        }
    }

    private static int GetEffectiveMulticast(BattleRuntime runtime, CardBattleState card)
    {
        var multicast = card.GetCombatAttribute(GameAttributeKeys.Multicast);
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

    private static void EnqueueEchoes(BattleRuntime runtime, PendingAbility origin)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAbilityActivated)
                Enqueue(runtime, source, ability, true);
        }
    }

    private static void ApplyEffect(BattleRuntime runtime, PendingAbility pending, EffectDefinition effect)
    {
        var definition = pending.Ability.Definition;
        var targetSide = definition.Target == AbilityTarget.EnemyHero
            ? Opposite(pending.Source.Side) : pending.Source.Side;
        var hero = runtime.GetHero(targetSide);
        switch (effect)
        {
            case DamageEffectDefinition damage:
                ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, damage.Amount, damage.BypassArmor,
                    pending.Source is SkillBattleState ? DamageSourceKind.Skill : DamageSourceKind.Card);
                if (!pending.IsEcho) EnqueueDamageEchoes(runtime);
                break;
            case MaxHealthPercentDamageEffectDefinition percentDamage:
                var amount = checked((int)((long)hero.MaxHealth * percentDamage.Percent / 100));
                ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, amount, percentDamage.BypassArmor,
                    pending.Source is SkillBattleState ? DamageSourceKind.Skill : DamageSourceKind.Card);
                if (!pending.IsEcho) EnqueueDamageEchoes(runtime);
                break;
            case AttributeDamageEffectDefinition attributeDamage:
                ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    GetEffectiveCombatAttribute(runtime, pending.Source, attributeDamage.AttributeKey),
                    attributeDamage.BypassArmor,
                    pending.Source is SkillBattleState ? DamageSourceKind.Skill : DamageSourceKind.Card);
                if (!pending.IsEcho) EnqueueDamageEchoes(runtime);
                break;
            case HealEffectDefinition heal:
                hero.Health = Math.Min(hero.MaxHealth, checked(hero.Health + heal.Amount));
                break;
            case ArmorEffectDefinition armor:
                hero.Armor = checked(hero.Armor + armor.Amount);
                break;
            case GainSourceHeroArmorEffectDefinition sourceArmor:
                var alliedHero = runtime.GetHero(pending.Source.Side);
                alliedHero.Armor = checked(alliedHero.Armor + sourceArmor.Amount);
                break;
            case GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor:
                var armorTarget = runtime.GetHero(pending.Source.Side);
                armorTarget.Armor = checked(
                    armorTarget.Armor + pending.Source.GetCombatAttribute(attributeArmor.AttributeKey));
                break;
            case GainArmorEqualToManaSpentEffectDefinition:
                var sourceHero = runtime.GetHero(pending.Source.Side);
                sourceHero.Armor = checked(sourceHero.Armor + sourceHero.ManaSpent);
                break;
            case ApplyStatusEffectDefinition status:
                ApplyStatus(pending.Source, hero, status);
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, status.Status, status.Amount));
                break;
            case ApplyStatusToAdjacentAlliedCardsEffectDefinition adjacent:
                if (pending.Source is not CardBattleState adjacentSource)
                    throw new InvalidOperationException("Adjacent card effects require a card source.");
                ApplyStatusToAdjacentAlliedCards(runtime, adjacentSource, adjacent);
                break;
            case DestroyCardEffectDefinition destroy:
                if (pending.Source is not CardBattleState destroySource)
                    throw new InvalidOperationException("Self-destruction requires a card source.");
                DestroyCard(runtime, destroySource, pending.Source.EntityId, destroy.Permanent);
                break;
            case DestroyRandomEnemyCardEffectDefinition randomDestroy:
                DestroyRandomEnemyCard(runtime, pending.Source, randomDestroy);
                break;
            case IncreaseSourceCooldownEffectDefinition cooldown:
                if (!cooldown.FirstActivationOnly || pending.Source.ActivationCount == 0)
                {
                    pending.Source.CooldownBonusTicks = checked(
                        pending.Source.CooldownBonusTicks + cooldown.AmountTicks);
                    foreach (var ability in pending.Source.Abilities)
                    {
                        if (ability.Definition.Activation == AbilityActivation.Active)
                            ability.RemainingCooldownUnits = checked(
                                ability.RemainingCooldownUnits + cooldown.AmountTicks * 2);
                    }
                }
                break;
            case ModifyTaggedAlliedCardsAttributeEffectDefinition modifier:
                foreach (var card in runtime.Cards)
                {
                    if (card.Side != pending.Source.Side || card.Destroyed || card.IsOnBench
                        || !card.Tags.Contains(modifier.RequiredTag)
                        || !card.SupportsCombatAttribute(modifier.AttributeKey))
                    {
                        continue;
                    }
                    var currentValue = card.AddCombatAttribute(modifier.AttributeKey, modifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(
                        runtime.Tick,
                        card.EntityId,
                        modifier.AttributeKey,
                        modifier.Amount,
                        currentValue));
                }
                break;
        }
    }

    private static int GetEffectiveCombatAttribute(BattleRuntime runtime, IBattleAbilitySource source, StringName key)
    {
        var value = source.GetCombatAttribute(key);
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
        return value;
    }

    private static void DestroyRandomEnemyCard(
        BattleRuntime runtime,
        IBattleAbilitySource source,
        DestroyRandomEnemyCardEffectDefinition effect)
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
        DestroyCard(runtime, candidates[runtime.NextRandomIndex(candidates.Count)], source.EntityId, effect.Permanent);
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

    private static bool HasEffectiveTag(BattleRuntime runtime, CardBattleState card, StringName tag)
    {
        if (card.Destroyed) return card.DestroyedTags.Contains(tag);
        if (card.Tags.Contains(tag)) return true;
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side == card.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantTagToEnemySizeCardsEffectDefinition aura
                    && aura.Tag == tag
                    && (int)aura.Size == card.OccupiedSlots)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void DestroyCard(BattleRuntime runtime, CardBattleState target, EntityId sourceId, bool permanent)
    {
        if (target.Destroyed) return;
        foreach (var tag in target.Tags) target.DestroyedTags.Add(tag);
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side == target.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantTagToEnemySizeCardsEffectDefinition aura
                    && (int)aura.Size == target.OccupiedSlots)
                {
                    target.DestroyedTags.Add(aura.Tag);
                }
            }
        }
        target.Destroyed = true;
        runtime.Events.Add(new CardDestroyedEvent(runtime.Tick, target.EntityId, target.Side, sourceId));
        if (permanent) runtime.PermanentChanges.Add(new PermanentChange(target.EntityId, "Destroy"));
    }

    private static void ApplyStatus(IBattleAbilitySource source, HeroBattleState hero, ApplyStatusEffectDefinition effect)
    {
        switch (effect.Status)
        {
            case BattleStatus.Burn: hero.Burn = checked(hero.Burn + effect.Amount); break;
            case BattleStatus.Poison: hero.Poison = checked(hero.Poison + effect.Amount); break;
            case BattleStatus.HealthRegen: hero.HealthRegen = checked(hero.HealthRegen + effect.Amount); break;
            case BattleStatus.ManaRegen: hero.ManaRegen = checked(hero.ManaRegen + effect.Amount); break;
            case BattleStatus.HasteDuration when source is CardBattleState card:
                card.HasteDuration = checked(card.HasteDuration + effect.Amount); break;
            case BattleStatus.SlowDuration when source is CardBattleState card:
                card.SlowDuration = checked(card.SlowDuration + effect.Amount); break;
            case BattleStatus.ImmobilizeDuration when source is CardBattleState card:
                card.ImmobilizeDuration = checked(card.ImmobilizeDuration + effect.Amount); break;
            case BattleStatus.HasteDuration or BattleStatus.SlowDuration or BattleStatus.ImmobilizeDuration:
                throw new InvalidOperationException("Card status requires a card source.");
        }
    }

    private static void ApplyStatusToAdjacentAlliedCards(
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
            ApplyStatus(card, runtime.GetHero(card.Side), new ApplyStatusEffectDefinition(effect.Status, amount));
            runtime.Events.Add(new StatusChangedEvent(runtime.Tick, effect.Status, amount));
        }
    }

    private static void EnqueueDamageEchoes(BattleRuntime runtime)
    {
        foreach (var source in runtime.AbilitySources)
        foreach (var ability in source.Abilities)
        {
            if (source.Destroyed || (source.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnDamageDealt)
                Enqueue(runtime, source, ability, true);
        }
    }

    private static void SettleHeroStatuses(BattleRuntime runtime)
    {
        SettleHero(runtime, SideId.Player, runtime.PlayerHero);
        SettleHero(runtime, SideId.Opponent, runtime.OpponentHero);
    }

    private static void SettleHero(BattleRuntime runtime, SideId side, HeroBattleState hero)
    {
        if (runtime.Tick.Value % 10 == 0)
        {
            if (hero.Burn > 0) ApplyDamage(runtime, hero.EntityId, side, hero, hero.Burn, false, DamageSourceKind.Status);
            if (hero.Poison > 0) ApplyDamage(runtime, hero.EntityId, side, hero, hero.Poison, true, DamageSourceKind.Status);
            hero.Health = Math.Min(hero.MaxHealth, checked(hero.Health + hero.HealthRegen));
            hero.Mana = Math.Min(hero.MaxMana, checked(hero.Mana + hero.ManaRegen));
            hero.Poison /= 2;
        }
        if (runtime.Tick.Value % 2 == 0) hero.Burn = Math.Max(0, hero.Burn - 1);
    }

    private static void ApplyEclipse(BattleRuntime runtime, BattleSetup setup)
    {
        if (runtime.Tick.Value < setup.EclipseTime.Value || runtime.Tick.Value % 10 != 0) return;
        var seconds = (runtime.Tick.Value - setup.EclipseTime.Value) / 10;
        var damage = 1 << (int)Math.Min(seconds, 30);
        ApplyDamage(runtime, runtime.PlayerHero.EntityId, SideId.Player, runtime.PlayerHero, damage, true, DamageSourceKind.Eclipse);
        ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Opponent, runtime.OpponentHero, damage, true, DamageSourceKind.Eclipse);
    }

    private static void ApplyDamage(
        BattleRuntime runtime,
        EntityId source,
        SideId targetSide,
        HeroBattleState target,
        int amount,
        bool bypassArmor,
        DamageSourceKind sourceKind = DamageSourceKind.Card)
    {
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
            sourceKind));
    }

    private static BattleResult? TryCreateDefeatResult(BattleRuntime runtime)
    {
        var player = runtime.PlayerHero.Health <= 0; var opponent = runtime.OpponentHero.Health <= 0;
        if (!player && !opponent) return null;
        if (player) runtime.Events.Add(new HeroDefeatedEvent(runtime.Tick, SideId.Player));
        if (opponent) runtime.Events.Add(new HeroDefeatedEvent(runtime.Tick, SideId.Opponent));
        if (player && opponent) return CreateResult(runtime, BattleOutcome.PlayerVictory, BattleEndReason.SimultaneousDefeat);
        return CreateResult(runtime, opponent ? BattleOutcome.PlayerVictory : BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated);
    }

    private static BattleResult CreateResult(BattleRuntime runtime, BattleOutcome outcome, BattleEndReason reason)
    {
        runtime.Events.Add(new BattleEndedEvent(runtime.Tick, reason));
        return new BattleResult(outcome, reason, runtime.Tick, runtime.PlayerHero.Health, runtime.OpponentHero.Health,
            runtime.Events.AsReadOnly(), runtime.PermanentChanges.AsReadOnly());
    }

    private static SideId Opposite(SideId side) => side == SideId.Player ? SideId.Opponent : SideId.Player;
}
