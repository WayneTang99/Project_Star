using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

namespace Project_Star.Domain.Definitions;

public abstract class HeroDefinition
{
    protected HeroDefinition(EntityAttributes<HeroIdentityAttributes> attributes, TagSet tags)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<HeroIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }
}

public abstract class CardDefinition
{
    protected CardDefinition(
        EntityAttributes<CardIdentityAttributes> attributes,
        TagSet tags,
        IReadOnlyList<AbilityDefinition>? abilities = null)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = TagSet.ForCard(attributes.Identity.Size, tags);
        Abilities = abilities is null
            ? Array.Empty<AbilityDefinition>()
            : new List<AbilityDefinition>(abilities).AsReadOnly();
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<CardIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }

    public virtual int ValueCoefficient => 2;

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
