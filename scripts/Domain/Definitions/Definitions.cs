using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

namespace Project_Star.Domain.Definitions;

public abstract class HeroDefinition
{
    protected HeroDefinition(EntityAttributes<HeroIdentityAttributes> attributes)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<HeroIdentityAttributes> Attributes { get; }
}

public abstract class CardDefinition
{
    protected CardDefinition(
        EntityAttributes<CardIdentityAttributes> attributes,
        TagSet tags,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        int initialLevel = 1,
        IReadOnlyList<CardLevelDefinition>? levels = null,
        CardOnSellRewardDefinition? onSellReward = null)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = TagSet.ForCard(attributes.Identity.Size, tags);
        Abilities = abilities is null
            ? Array.Empty<AbilityDefinition>()
            : new List<AbilityDefinition>(abilities).AsReadOnly();
        InitialLevel = initialLevel;
        var configuredLevels = new Dictionary<int, CardLevelDefinition>();
        if (levels is not null)
        {
            foreach (var level in levels)
            {
                if (!configuredLevels.TryAdd(level.Level, level))
                    throw new ArgumentException($"Duplicate card level configuration '{level.Level}'.", nameof(levels));
            }
        }
        if (initialLevel is < 1 or > 5 || (configuredLevels.Count > 0 && !configuredLevels.ContainsKey(initialLevel)))
            throw new ArgumentOutOfRangeException(nameof(initialLevel), "Initial level must have a card level configuration.");
        Levels = configuredLevels;
        OnSellReward = onSellReward;
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<CardIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }

    public int InitialLevel { get; }

    public IReadOnlyDictionary<int, CardLevelDefinition> Levels { get; }

    public CardOnSellRewardDefinition? OnSellReward { get; }

    public bool SupportsLevel(int level) => Levels.Count == 0 ? level is >= 1 and <= 4 : Levels.ContainsKey(level);

    public CardLevelDefinition? GetLevel(int level) => Levels.TryGetValue(level, out var value) ? value : null;

    public virtual int ValueCoefficient => 2;

}

public abstract record CardOnSellRewardDefinition;

// 出售时按标签随机获得卡牌的通用奖励定义（领域定义层）。
public sealed record RandomTaggedCardOnSellDefinition(StringName RequiredTag, bool SameLevel = true)
    : CardOnSellRewardDefinition;

// 单个卡牌等级的数值与能力配置（领域定义层）。
public sealed class CardLevelDefinition
{
    public CardLevelDefinition(
        int level,
        IReadOnlyDictionary<StringName, int>? baseCombatValues,
        IReadOnlyList<AbilityDefinition> abilities,
        int? initialValue = null,
        int acquiredValueBonus = 0)
    {
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentNullException.ThrowIfNull(abilities);
        if (initialValue < 0) throw new ArgumentOutOfRangeException(nameof(initialValue));
        ArgumentOutOfRangeException.ThrowIfNegative(acquiredValueBonus);
        Level = level;
        BaseCombatValues = baseCombatValues is null
            ? new Dictionary<StringName, int>()
            : new Dictionary<StringName, int>(baseCombatValues);
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
        InitialValue = initialValue;
        AcquiredValueBonus = acquiredValueBonus;
    }

    public int Level { get; }

    public IReadOnlyDictionary<StringName, int> BaseCombatValues { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }

    public int? InitialValue { get; }

    public int AcquiredValueBonus { get; }
}

public abstract class EncounterDefinition
{
    protected EncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        int minimumRound,
        int maximumRound,
        EncounterKind kind = EncounterKind.Other,
        int baseWeight = 1)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        MinimumRound = minimumRound;
        MaximumRound = maximumRound;
        Kind = kind;
        BaseWeight = baseWeight;
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<EncounterIdentityAttributes> Attributes { get; }

    public int MinimumRound { get; }

    public int MaximumRound { get; }
    public EncounterKind Kind { get; }
    public int BaseWeight { get; }
}

public enum EncounterKind
{
    Shop = 0,
    Monster = 1,
    Pvp = 2,
    Other = 3,
}

internal static class DefinitionFreezer
{
    public static void Freeze<TIdentity>(EntityAttributes<TIdentity> attributes) where TIdentity : class
    {
        attributes.Persistent.Freeze();
        attributes.BaseCombat.Freeze();
    }
}
