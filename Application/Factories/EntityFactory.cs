using System;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Factories;

/// <summary>Creates independent match instances from immutable definitions.</summary>
public sealed class EntityFactory
{
    public HeroInstance CreateHero(HeroDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new HeroInstance(EntityId.New(), Copy(definition.Attributes), definition.Tags);
    }

    public CardInstance CreateCard(CardDefinition definition, int level = 1)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Card level must be at least one.");
        }

        var attributes = Copy(definition.Attributes);
        attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, level);
        return new CardInstance(EntityId.New(), attributes, definition.Tags, definition.Abilities);
    }

    private static EntityAttributes<TIdentity> Copy<TIdentity>(EntityAttributes<TIdentity> source)
        where TIdentity : class =>
        new(source.Identity, source.Persistent.CreateMutableCopy(), source.BaseCombat.CreateMutableCopy());
}
