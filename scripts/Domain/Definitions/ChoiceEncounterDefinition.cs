using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 可扩展选项遭遇及其通用效果定义（领域定义层）。
public abstract class ChoiceEncounterDefinition : EncounterDefinition
{
    protected ChoiceEncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        int minimumRound,
        int maximumRound,
        IReadOnlyList<EncounterOptionDefinition> options,
        IReadOnlyList<EncounterOptionSlotDefinition>? optionSlots = null,
        int level = 1,
        int baseWeight = 1)
        : base(attributes, minimumRound, maximumRound, EncounterKind.Other, baseWeight)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0 || level < 1) throw new ArgumentException("A choice encounter configuration is invalid.");
        Options = new List<EncounterOptionDefinition>(options).AsReadOnly();
        OptionSlots = optionSlots is null
            ? CreateGuaranteedSlots(options)
            : new List<EncounterOptionSlotDefinition>(optionSlots).AsReadOnly();
        if (OptionSlots.Count == 0) throw new ArgumentException("A choice encounter requires at least one option slot.", nameof(optionSlots));
        Level = level;
    }

    public IReadOnlyList<EncounterOptionDefinition> Options { get; }
    public IReadOnlyList<EncounterOptionSlotDefinition> OptionSlots { get; }
    public int Level { get; }

    private static IReadOnlyList<EncounterOptionSlotDefinition> CreateGuaranteedSlots(
        IReadOnlyList<EncounterOptionDefinition> options)
    {
        var slots = new List<EncounterOptionSlotDefinition>(options.Count);
        foreach (var option in options)
            slots.Add(new EncounterOptionSlotDefinition([new WeightedEncounterOptionDefinition(option.Key, 1)]));
        return slots.AsReadOnly();
    }
}

// 一个展示位及其加权候选选项（领域定义层）。
public sealed class EncounterOptionSlotDefinition
{
    public EncounterOptionSlotDefinition(IReadOnlyList<WeightedEncounterOptionDefinition> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) throw new ArgumentException("An option slot requires candidates.", nameof(candidates));
        Candidates = new List<WeightedEncounterOptionDefinition>(candidates).AsReadOnly();
    }

    public IReadOnlyList<WeightedEncounterOptionDefinition> Candidates { get; }
}

public sealed record WeightedEncounterOptionDefinition(StringName OptionKey, int Weight);

// 单个遭遇选项及其效果列表（领域定义层）。
public sealed class EncounterOptionDefinition
{
    public EncounterOptionDefinition(StringName key, string displayName, IReadOnlyList<EncounterOptionEffectDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (key.IsEmpty || string.IsNullOrWhiteSpace(displayName) || effects.Count == 0)
            throw new ArgumentException("Encounter option configuration is invalid.");
        Key = key;
        DisplayName = displayName;
        Effects = new List<EncounterOptionEffectDefinition>(effects).AsReadOnly();
    }

    public StringName Key { get; }
    public string DisplayName { get; }
    public IReadOnlyList<EncounterOptionEffectDefinition> Effects { get; }
}

public abstract record EncounterOptionEffectDefinition;

// 按当前英雄等级倍数永久增加英雄战斗属性（领域定义层）。
public sealed record ModifyHeroCombatAttributeByLevelEffectDefinition(StringName AttributeKey, int AmountPerLevel)
    : EncounterOptionEffectDefinition;

public sealed record GainWealthEncounterOptionEffectDefinition(int Amount) : EncounterOptionEffectDefinition;

public sealed record GrantRandomFactionCardEncounterOptionEffectDefinition(CardSize Size, int Level)
    : EncounterOptionEffectDefinition;

public sealed record GrantRandomTaggedCardEncounterOptionEffectDefinition(StringName RequiredTag, CardSize Size, int Level)
    : EncounterOptionEffectDefinition;

public sealed record BuyRandomOtherFactionCardEncounterOptionEffectDefinition(CardSize Size, int Level, int Cost)
    : EncounterOptionEffectDefinition;

public sealed record GrantNextBattleMaxHealthByLevelEncounterOptionEffectDefinition(int AmountPerLevel)
    : EncounterOptionEffectDefinition;

public sealed record ModifyBattlefieldCardAttributeEncounterOptionEffectDefinition(StringName AttributeKey, int Amount)
    : EncounterOptionEffectDefinition;
