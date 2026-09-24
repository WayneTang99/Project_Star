using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

namespace Project_Star.Domain.Definitions;

// 套装阈值与其解锁能力的静态定义（领域定义层）。
public abstract class CardSetDefinition
{
    protected CardSetDefinition(
        EntityAttributes<CardSetIdentityAttributes> attributes,
        IReadOnlyList<CardSetThresholdDefinition> thresholds)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        ArgumentNullException.ThrowIfNull(thresholds);
        var ordered = new List<CardSetThresholdDefinition>(thresholds);
        if (ordered.Count == 0) throw new ArgumentException("A set needs at least one threshold.", nameof(thresholds));
        ordered.Sort((left, right) => left.RequiredDistinctCards.CompareTo(right.RequiredDistinctCards));
        for (var index = 1; index < ordered.Count; index++)
            if (ordered[index - 1].RequiredDistinctCards == ordered[index].RequiredDistinctCards)
                throw new ArgumentException("Set thresholds must be unique.", nameof(thresholds));
        Thresholds = ordered.AsReadOnly();
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<CardSetIdentityAttributes> Attributes { get; }

    public IReadOnlyList<CardSetThresholdDefinition> Thresholds { get; }
}

// 达到不同卡牌数量后累计生效的一档效果（领域定义层）。
public sealed class CardSetThresholdDefinition
{
    public CardSetThresholdDefinition(
        int requiredDistinctCards,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        if (requiredDistinctCards < 1) throw new ArgumentOutOfRangeException(nameof(requiredDistinctCards));
        ArgumentNullException.ThrowIfNull(abilities);
        if (abilities.Count == 0)
            throw new ArgumentException("A threshold needs an ability.", nameof(abilities));
        SkillDefinition.ValidateNonCardAbilities(abilities, allowPersistent: true);
        RequiredDistinctCards = requiredDistinctCards;
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
    }

    public int RequiredDistinctCards { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }
}
