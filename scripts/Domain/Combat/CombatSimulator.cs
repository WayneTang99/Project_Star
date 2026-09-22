using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Combat;

/// <summary>Runs deterministic combat, including abilities, statuses, eclipse, and extinction.</summary>
public sealed class CombatSimulator
{
    public BattleResult Simulate(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var runtime = new BattleRuntime(setup);
        runtime.Events.Add(new BattleStartedEvent(runtime.Tick));
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
        CardBattleState card,
        BattleAbilityState ability,
        bool echo,
        bool multicast = false)
    {
        runtime.Queue.Enqueue(new PendingAbility(card, ability, echo, multicast, runtime.Tick));
        runtime.Events.Add(new AbilityQueuedEvent(runtime.Tick, card.EntityId, card.Side));
    }

    private static void ResolveQueue(BattleRuntime runtime)
    {
        while (runtime.Queue.Count > 0)
        {
            var pending = runtime.Queue.Dequeue();
            if (pending.Source.Destroyed) continue;
            var hero = runtime.GetHero(pending.Source.Side);
            var definition = pending.Ability.Definition;
            if (!pending.IsMulticast && hero.Mana < definition.ManaCost) continue;
            if (!pending.IsMulticast) hero.Mana -= definition.ManaCost;
            if (definition.ManaCost > 0 && !pending.IsMulticast)
                runtime.Events.Add(new ManaChangedEvent(runtime.Tick, pending.Source.Side, -definition.ManaCost, hero.Mana));
            if (!pending.IsEcho && !pending.IsMulticast)
                pending.Ability.RemainingCooldownUnits = (definition.CooldownTicks + pending.Source.CooldownBonusTicks) * 2;
            runtime.Events.Add(new AbilityActivatedEvent(runtime.Tick, pending.Source.EntityId, pending.Source.Side, pending.IsEcho));
            if (!pending.IsEcho && !pending.IsMulticast)
                for (var repeat = 0; repeat < pending.Source.GetCombatAttribute(GameAttributeKeys.Multicast); repeat++)
                    Enqueue(runtime, pending.Source, pending.Ability, false, true);
            foreach (var effect in definition.Effects) ApplyEffect(runtime, pending, effect);
            if (!pending.IsEcho) pending.Source.ActivationCount++;
            if (!pending.IsEcho) EnqueueEchoes(runtime, pending);
        }
    }

    private static void EnqueueEchoes(BattleRuntime runtime, PendingAbility origin)
    {
        foreach (var card in runtime.Cards)
        foreach (var ability in card.Abilities)
        {
            if (card.Destroyed || (card.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnAbilityActivated)
                Enqueue(runtime, card, ability, true);
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
                ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, damage.Amount, damage.BypassArmor);
                if (!pending.IsEcho) EnqueueDamageEchoes(runtime);
                break;
            case MaxHealthPercentDamageEffectDefinition percentDamage:
                var amount = checked((int)((long)hero.MaxHealth * percentDamage.Percent / 100));
                ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, amount, percentDamage.BypassArmor);
                if (!pending.IsEcho) EnqueueDamageEchoes(runtime);
                break;
            case AttributeDamageEffectDefinition attributeDamage:
                ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    pending.Source.GetCombatAttribute(attributeDamage.AttributeKey),
                    attributeDamage.BypassArmor);
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
            case ApplyStatusEffectDefinition status:
                ApplyStatus(pending.Source, hero, status);
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, status.Status, status.Amount));
                break;
            case DestroyCardEffectDefinition destroy:
                pending.Source.Destroyed = true;
                if (destroy.Permanent) runtime.PermanentChanges.Add(new PermanentChange(pending.Source.EntityId, "Destroy"));
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

    private static void ApplyStatus(CardBattleState card, HeroBattleState hero, ApplyStatusEffectDefinition effect)
    {
        switch (effect.Status)
        {
            case BattleStatus.Burn: hero.Burn = checked(hero.Burn + effect.Amount); break;
            case BattleStatus.Poison: hero.Poison = checked(hero.Poison + effect.Amount); break;
            case BattleStatus.HealthRegen: hero.HealthRegen = checked(hero.HealthRegen + effect.Amount); break;
            case BattleStatus.ManaRegen: hero.ManaRegen = checked(hero.ManaRegen + effect.Amount); break;
            case BattleStatus.HasteDuration: card.HasteDuration = checked(card.HasteDuration + effect.Amount); break;
            case BattleStatus.SlowDuration: card.SlowDuration = checked(card.SlowDuration + effect.Amount); break;
            case BattleStatus.ImmobilizeDuration: card.ImmobilizeDuration = checked(card.ImmobilizeDuration + effect.Amount); break;
        }
    }

    private static void EnqueueDamageEchoes(BattleRuntime runtime)
    {
        foreach (var card in runtime.Cards)
        foreach (var ability in card.Abilities)
        {
            if (card.Destroyed || (card.IsOnBench && !ability.Definition.AllowsBench)) continue;
            if (ability.Definition.Activation == AbilityActivation.EchoOnDamageDealt)
                Enqueue(runtime, card, ability, true);
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
            if (hero.Burn > 0) ApplyDamage(runtime, hero.EntityId, side, hero, hero.Burn, false);
            if (hero.Poison > 0) ApplyDamage(runtime, hero.EntityId, side, hero, hero.Poison, true);
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
        ApplyDamage(runtime, runtime.PlayerHero.EntityId, SideId.Player, runtime.PlayerHero, damage, true);
        ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Opponent, runtime.OpponentHero, damage, true);
    }

    private static void ApplyDamage(BattleRuntime runtime, EntityId source, SideId targetSide, HeroBattleState target, int amount, bool bypassArmor)
    {
        var absorbed = bypassArmor ? 0 : Math.Min(target.Armor, amount);
        target.Armor -= absorbed;
        var healthDamage = amount - absorbed;
        target.Health = Math.Max(0, target.Health - healthDamage);
        runtime.Events.Add(new DamageDealtEvent(runtime.Tick, source, targetSide, amount, absorbed, healthDamage, target.Health));
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
