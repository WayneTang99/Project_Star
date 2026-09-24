using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

public enum AbilityActivation
{
    Active = 0,
    EchoOnAbilityActivated = 1,
    EchoOnDamageDealt = 2,
    PassiveAura = 3,
    PassiveOnBattleStart = 4,
    PassiveWhileEnabled = 5,
    EchoOnFirstAlliedCardActivated = 6,
}

public enum AbilityTarget
{
    EnemyHero = 0,
    AlliedHero = 1,
    SelfCard = 2,
    SourceGroupCards = 3,
    OtherBattlefieldCards = 4,
    AllBattlefieldCards = 5,
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

public sealed record ModifyAttributeEffectDefinition(StringName AttributeKey, int Amount) : EffectDefinition;

public sealed record DamageEffectDefinition(int Amount, bool BypassArmor = false) : EffectDefinition;

// 按来源方英雄等级与固定系数造成伤害（领域战斗层）。
public sealed record SourceHeroLevelScaledDamageEffectDefinition(int Multiplier, bool BypassArmor = false)
    : EffectDefinition;

public sealed record MaxHealthPercentDamageEffectDefinition(int Percent, bool BypassArmor = false) : EffectDefinition;

public sealed record AttributeDamageEffectDefinition(StringName AttributeKey, bool BypassArmor = false)
    : EffectDefinition;

// 按来源方英雄当前生命比例缩放来源属性伤害（领域战斗层）。
public sealed record SourceHeroHealthScaledAttributeDamageEffectDefinition(
    StringName AttributeKey, bool BypassArmor = false) : EffectDefinition;

public sealed record SourceHeroArmorDamageEffectDefinition(
    bool BypassArmor = false,
    StringName? BonusAttributeKey = null) : EffectDefinition;

public sealed record HealEffectDefinition(int Amount) : EffectDefinition;

public sealed record ArmorEffectDefinition(int Amount) : EffectDefinition;

public sealed record GainSourceHeroArmorEffectDefinition(int Amount) : EffectDefinition;

public sealed record GainSourceHeroArmorFromAttributeEffectDefinition(StringName AttributeKey)
    : EffectDefinition;

public sealed record GainArmorEqualToManaSpentEffectDefinition : EffectDefinition;

public sealed record GrantMulticastToAlliedElementCardsEffectDefinition(StringName ElementKey, int Amount)
    : EffectDefinition;

// 随机充能己方另一件指定属性卡牌（领域战斗层）。
public sealed record ChargeRandomOtherAlliedElementCardEffectDefinition(
    StringName ElementKey,
    int AmountTicks) : EffectDefinition;

// 使敌方指定尺寸卡牌在战斗中获得标签（领域战斗层）。
public sealed record GrantTagToEnemySizeCardsEffectDefinition(CardSize Size, StringName Tag)
    : EffectDefinition;

// 按敌方存活的指定标签卡牌数量增加来源属性（领域战斗层）。
public sealed record IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition(
    StringName AttributeKey,
    StringName RequiredTag,
    int Amount) : EffectDefinition;

public sealed record ApplyStatusEffectDefinition(BattleStatus Status, int Amount) : EffectDefinition;

public sealed record ApplyStatusToAdjacentAlliedCardsEffectDefinition(
    BattleStatus Status,
    int Amount,
    StringName BonusTag,
    int BonusMultiplier = 2) : EffectDefinition;

// 当相邻友方指定标签卡牌获得指定状态时，增加该目标的战斗属性。
public sealed record ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition(
    BattleStatus Status,
    StringName RequiredTag,
    StringName AttributeKey,
    int Amount) : EffectDefinition;

public sealed record DestroyCardEffectDefinition(bool Permanent) : EffectDefinition;

// 随机摧毁符合尺寸及任一标签条件的敌方卡牌（领域战斗层）。
public sealed record DestroyRandomEnemyCardEffectDefinition(
    IReadOnlyList<StringName> RequiredAnyTags,
    IReadOnlyList<CardSize> AllowedSizes,
    bool Permanent = false) : EffectDefinition;

// 按本场已摧毁的指定标签卡牌数量倍增来源属性（领域战斗层）。
public sealed record MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition(
    StringName AttributeKey,
    IReadOnlyList<StringName> RequiredAnyTags,
    int Multiplier = 2) : EffectDefinition;

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
        if (activation != AbilityActivation.Active && manaCost != 0)
            throw new ArgumentException("Only active abilities can consume mana.", nameof(manaCost));
        if ((activation is AbilityActivation.PassiveAura or AbilityActivation.PassiveOnBattleStart
            or AbilityActivation.PassiveWhileEnabled)
            && cooldownTicks != 0)
            throw new ArgumentException("A passive ability cannot have a cooldown.", nameof(cooldownTicks));
        if (activation == AbilityActivation.PassiveWhileEnabled
            && (target is not AbilityTarget.SelfCard and not AbilityTarget.SourceGroupCards and not AbilityTarget.OtherBattlefieldCards
                and not AbilityTarget.AllBattlefieldCards and not AbilityTarget.AlliedHero
                || allowsBench))
            throw new ArgumentException("Persistent ability target is invalid.", nameof(target));

        Key = key;
        Activation = activation;
        Target = target;
        ManaCost = manaCost;
        CooldownTicks = cooldownTicks;
        foreach (var effect in effects)
        {
            if ((activation == AbilityActivation.PassiveWhileEnabled) != (effect is ModifyAttributeEffectDefinition))
                throw new ArgumentException("Persistent abilities require attribute modifiers only.", nameof(effects));
            var amount = effect switch
            {
                DamageEffectDefinition value => value.Amount,
                SourceHeroLevelScaledDamageEffectDefinition value => value.Multiplier,
                MaxHealthPercentDamageEffectDefinition value => value.Percent,
                IncreaseSourceCooldownEffectDefinition value => value.AmountTicks,
                ModifyTaggedAlliedCardsAttributeEffectDefinition value => value.Amount,
                HealEffectDefinition value => value.Amount,
                ArmorEffectDefinition value => value.Amount,
                GainSourceHeroArmorEffectDefinition value => value.Amount,
                ApplyStatusEffectDefinition value => value.Amount,
                ApplyStatusToAdjacentAlliedCardsEffectDefinition value => value.Amount,
                ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition value => value.Amount,
                GrantMulticastToAlliedElementCardsEffectDefinition value => value.Amount,
                ChargeRandomOtherAlliedElementCardEffectDefinition value => value.AmountTicks,
                IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition value => value.Amount,
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
            if (effect is ModifyAttributeEffectDefinition attributeModifier
                && (attributeModifier.AttributeKey.IsEmpty || attributeModifier.Amount == 0))
                throw new ArgumentException("Attribute modifier is invalid.", nameof(effects));
            if (effect is SourceHeroHealthScaledAttributeDamageEffectDefinition scaledDamage
                && scaledDamage.AttributeKey.IsEmpty)
                throw new ArgumentException("Scaled damage key cannot be empty.", nameof(effects));
            if (effect is GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor
                && attributeArmor.AttributeKey.IsEmpty)
            {
                throw new ArgumentException("Attribute armor key cannot be empty.", nameof(effects));
            }
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
            if (effect is ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition statusReaction
                && (activation != AbilityActivation.PassiveAura
                    || statusReaction.Status is not BattleStatus.HasteDuration
                        and not BattleStatus.SlowDuration
                        and not BattleStatus.ImmobilizeDuration
                    || statusReaction.RequiredTag.IsEmpty
                    || statusReaction.AttributeKey.IsEmpty
                    || statusReaction.Amount == 0))
            {
                throw new ArgumentException("Adjacent card status reaction configuration is invalid.", nameof(effects));
            }
            if (effect is GrantMulticastToAlliedElementCardsEffectDefinition multicastAura
                && multicastAura.ElementKey.IsEmpty)
            {
                throw new ArgumentException("Multicast aura element key cannot be empty.", nameof(effects));
            }
            if (effect is ChargeRandomOtherAlliedElementCardEffectDefinition charge
                && (charge.ElementKey.IsEmpty || charge.AmountTicks < 1))
            {
                throw new ArgumentException("Random card charge configuration is invalid.", nameof(effects));
            }
            if (effect is GrantTagToEnemySizeCardsEffectDefinition tagAura
                && (!Enum.IsDefined(tagAura.Size) || tagAura.Tag.IsEmpty))
            {
                throw new ArgumentException("Enemy card tag aura configuration is invalid.", nameof(effects));
            }
            if (effect is IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition taggedIncrease
                && (taggedIncrease.AttributeKey.IsEmpty || taggedIncrease.RequiredTag.IsEmpty))
            {
                throw new ArgumentException("Enemy tagged card attribute increase keys cannot be empty.", nameof(effects));
            }
            if (effect is DestroyRandomEnemyCardEffectDefinition randomDestroy
                && (randomDestroy.RequiredAnyTags.Count == 0
                    || randomDestroy.AllowedSizes.Count == 0
                    || HasEmptyKey(randomDestroy.RequiredAnyTags)))
            {
                throw new ArgumentException("Random card destruction filters cannot be empty.", nameof(effects));
            }
            if (effect is MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition multiplier
                && (multiplier.AttributeKey.IsEmpty
                    || multiplier.RequiredAnyTags.Count == 0
                    || HasEmptyKey(multiplier.RequiredAnyTags)
                    || multiplier.Multiplier < 1))
            {
                throw new ArgumentException("Destroyed card attribute multiplier is invalid.", nameof(effects));
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

    private static bool HasEmptyKey(IReadOnlyList<StringName> keys)
    {
        foreach (var key in keys)
            if (key.IsEmpty) return true;
        return false;
    }
}
