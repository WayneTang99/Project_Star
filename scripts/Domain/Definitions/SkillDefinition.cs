using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

public abstract class SkillDefinition
{
    protected SkillDefinition(
        EntityAttributes<SkillIdentityAttributes> attributes,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        int initialLevel = 1,
        IReadOnlyList<SkillLevelDefinition>? levels = null)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Abilities = abilities is null ? Array.Empty<AbilityDefinition>() : new List<AbilityDefinition>(abilities).AsReadOnly();
        ValidateNonCardAbilities(Abilities);
        var configuredLevels = new Dictionary<int, SkillLevelDefinition>();
        if (levels is not null)
            foreach (var level in levels)
                if (!configuredLevels.TryAdd(level.Level, level))
                    throw new ArgumentException($"Duplicate skill level configuration '{level.Level}'.", nameof(levels));
        if (initialLevel is < 1 or > 5 || (configuredLevels.Count > 0 && !configuredLevels.ContainsKey(initialLevel)))
            throw new ArgumentOutOfRangeException(nameof(initialLevel));
        foreach (var level in configuredLevels.Values) ValidateNonCardAbilities(level.Abilities);
        InitialLevel = initialLevel;
        Levels = configuredLevels;
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<SkillIdentityAttributes> Attributes { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
    public int InitialLevel { get; }
    public IReadOnlyDictionary<int, SkillLevelDefinition> Levels { get; }

    public bool SupportsLevel(int level) => Levels.Count == 0 ? level is >= 1 and <= 4 : Levels.ContainsKey(level);
    public SkillLevelDefinition? GetLevel(int level) => Levels.TryGetValue(level, out var value) ? value : null;

    internal static void ValidateNonCardAbilities(IReadOnlyList<AbilityDefinition> abilities, bool allowPersistent = false)
    {
        foreach (var ability in abilities)
        {
            if (ability.Activation == AbilityActivation.Active || ability.Target == AbilityTarget.SelfCard
                || ability.Activation == AbilityActivation.PassiveWhileEnabled && !allowPersistent
                || ability.Activation != AbilityActivation.PassiveWhileEnabled
                    && ability.Target is (AbilityTarget.SourceGroupCards or AbilityTarget.OtherBattlefieldCards
                        or AbilityTarget.AllBattlefieldCards))
                throw new ArgumentException("A non-card source requires a passive ability without a self-card target.", nameof(abilities));
            foreach (var effect in ability.Effects)
            {
                if (effect is ApplyStatusToAdjacentAlliedCardsEffectDefinition
                    or DestroyCardEffectDefinition
                    or IncreaseSourceCooldownEffectDefinition
                    || effect is ApplyStatusEffectDefinition status
                        && status.Status is BattleStatus.HasteDuration or BattleStatus.SlowDuration or BattleStatus.ImmobilizeDuration)
                {
                    throw new ArgumentException("Skill effect requires a card source.", nameof(abilities));
                }
            }
        }
    }
}

public sealed class SkillLevelDefinition
{
    public SkillLevelDefinition(
        int level,
        IReadOnlyDictionary<StringName, int>? baseCombatValues,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentNullException.ThrowIfNull(abilities);
        Level = level;
        BaseCombatValues = baseCombatValues is null
            ? new Dictionary<StringName, int>()
            : new Dictionary<StringName, int>(baseCombatValues);
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
    }

    public int Level { get; }
    public IReadOnlyDictionary<StringName, int> BaseCombatValues { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
}
