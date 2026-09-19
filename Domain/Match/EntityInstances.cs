using System;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;
using System.Collections.Generic;

namespace Project_Star.Domain.Match;

public sealed class HeroInstance
{
    public HeroInstance(EntityId id, EntityAttributes<HeroIdentityAttributes> attributes, TagSet tags)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    public EntityId Id { get; }

    public EntityAttributes<HeroIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }
}

public sealed class CardInstance
{
    public CardInstance(
        EntityId id,
        EntityAttributes<CardIdentityAttributes> attributes,
        TagSet tags,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
    }

    public EntityId Id { get; }

    public EntityAttributes<CardIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }
}
