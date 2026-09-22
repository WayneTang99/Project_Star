using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Combat;

public enum AbilityActivation
{
    Active = 0,
    EchoOnAbilityActivated = 1,
    EchoOnDamageDealt = 2,
}

public enum AbilityTarget
{
    EnemyHero = 0,
    AlliedHero = 1,
    SelfCard = 2,
}

public enum BattleStatus
{
    Burn = 0,
    Poison = 1,
    HealthRegen = 2,
    ManaRegen = 3,
    HasteDuration = 4,
    SlowDuration = 5,
    ImmobilizeDuration = 6,
}

public abstract record EffectDefinition;

public sealed record DamageEffectDefinition(int Amount, bool BypassArmor = false) : EffectDefinition;

public sealed record MaxHealthPercentDamageEffectDefinition(int Percent, bool BypassArmor = false) : EffectDefinition;

public sealed record AttributeDamageEffectDefinition(StringName AttributeKey, bool BypassArmor = false)
    : EffectDefinition;

public sealed record HealEffectDefinition(int Amount) : EffectDefinition;

public sealed record ArmorEffectDefinition(int Amount) : EffectDefinition;

public sealed record GainSourceHeroArmorEffectDefinition(int Amount) : EffectDefinition;

public sealed record GainArmorEqualToManaSpentEffectDefinition : EffectDefinition;

public sealed record ApplyStatusEffectDefinition(BattleStatus Status, int Amount) : EffectDefinition;

public sealed record ApplyStatusToAdjacentAlliedCardsEffectDefinition(
    BattleStatus Status,
    int Amount,
    StringName BonusTag,
    int BonusMultiplier = 2) : EffectDefinition;

public sealed record DestroyCardEffectDefinition(bool Permanent) : EffectDefinition;

public sealed record IncreaseSourceCooldownEffectDefinition(int AmountTicks, bool FirstActivationOnly = false)
    : EffectDefinition;

public sealed record ModifyTaggedAlliedCardsAttributeEffectDefinition(
    StringName RequiredTag,
    StringName AttributeKey,
    int Amount) : EffectDefinition;

public sealed class AbilityDefinition
{
    public AbilityDefinition(
        StringName key,
        AbilityActivation activation,
        AbilityTarget target,
        int manaCost,
        int cooldownTicks,
        IReadOnlyList<EffectDefinition> effects,
        bool allowsBench = false)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (key.IsEmpty || manaCost < 0 || cooldownTicks < 0 || effects.Count == 0)
        {
            throw new ArgumentException("Ability configuration is invalid.");
        }

        if (activation == AbilityActivation.Active && cooldownTicks < 1)
        {
            throw new ArgumentException("An active ability must have a positive cooldown.");
        }

        Key = key;
        Activation = activation;
        Target = target;
        ManaCost = manaCost;
        CooldownTicks = cooldownTicks;
        foreach (var effect in effects)
        {
            var amount = effect switch
            {
                DamageEffectDefinition value => value.Amount,
                MaxHealthPercentDamageEffectDefinition value => value.Percent,
                IncreaseSourceCooldownEffectDefinition value => value.AmountTicks,
                ModifyTaggedAlliedCardsAttributeEffectDefinition value => value.Amount,
                HealEffectDefinition value => value.Amount,
                ArmorEffectDefinition value => value.Amount,
                GainSourceHeroArmorEffectDefinition value => value.Amount,
                ApplyStatusEffectDefinition value => value.Amount,
                ApplyStatusToAdjacentAlliedCardsEffectDefinition value => value.Amount,
                _ => 0,
            };
            if (amount < 0) throw new ArgumentException("Effect amount cannot be negative.", nameof(effects));
            if (effect is MaxHealthPercentDamageEffectDefinition percentDamage
                && percentDamage.Percent is < 1 or > 100)
            {
                throw new ArgumentException("Max health damage percent must be between 1 and 100.", nameof(effects));
            }
            if (effect is AttributeDamageEffectDefinition attributeDamage && attributeDamage.AttributeKey.IsEmpty)
                throw new ArgumentException("Attribute damage key cannot be empty.", nameof(effects));
            if (effect is ModifyTaggedAlliedCardsAttributeEffectDefinition modifier
                && (modifier.RequiredTag.IsEmpty || modifier.AttributeKey.IsEmpty))
                throw new ArgumentException("Tagged card attribute modifier keys cannot be empty.", nameof(effects));
            if (effect is ApplyStatusToAdjacentAlliedCardsEffectDefinition adjacent
                && (adjacent.Status is not BattleStatus.HasteDuration
                    and not BattleStatus.SlowDuration
                    and not BattleStatus.ImmobilizeDuration
                    || adjacent.BonusTag.IsEmpty
                    || adjacent.BonusMultiplier < 1))
            {
                throw new ArgumentException("Adjacent card status configuration is invalid.", nameof(effects));
            }
        }
        Effects = new List<EffectDefinition>(effects).AsReadOnly();
        AllowsBench = allowsBench;
    }

    public StringName Key { get; }
    public AbilityActivation Activation { get; }
    public AbilityTarget Target { get; }
    public int ManaCost { get; }
    public int CooldownTicks { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }
    public bool AllowsBench { get; }
}
