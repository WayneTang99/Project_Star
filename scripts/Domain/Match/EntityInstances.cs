using System;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;
using System.Collections.Generic;

namespace Project_Star.Domain.Match;

public sealed class HeroInstance
{
    public HeroInstance(EntityId id, EntityAttributes<HeroIdentityAttributes> attributes)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    public EntityId Id { get; }

    public EntityAttributes<HeroIdentityAttributes> Attributes { get; }
}

public sealed class CardInstance
{
    public CardInstance(
        EntityId id,
        EntityAttributes<CardIdentityAttributes> attributes,
        TagSet tags,
        IReadOnlyList<AbilityDefinition> abilities,
        CardOnSellRewardDefinition? onSellReward)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        OnSellReward = onSellReward;
    }

    public EntityId Id { get; }

    public EntityAttributes<CardIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; private set; }

    public CardOnSellRewardDefinition? OnSellReward { get; }

    internal void ReplaceAbilities(IReadOnlyList<AbilityDefinition> abilities) =>
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
}
