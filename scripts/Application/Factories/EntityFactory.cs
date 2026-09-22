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
        return new HeroInstance(EntityId.New(), Copy(definition.Attributes));
    }

    public CardInstance CreateCard(CardDefinition definition, int? level = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var selectedLevel = level ?? definition.InitialLevel;
        if (!definition.SupportsLevel(selectedLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(level), $"Card does not provide level {selectedLevel}.");
        }

        var attributes = Copy(definition.Attributes);
        attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, selectedLevel);
        var levelDefinition = definition.GetLevel(selectedLevel);
        if (levelDefinition is not null)
        {
            foreach (var (key, value) in levelDefinition.BaseCombatValues)
                attributes.BaseCombat.SetBaseValue(key, value);
        }
        return new CardInstance(
            EntityId.New(),
            attributes,
            definition.Tags,
            levelDefinition?.Abilities ?? definition.Abilities,
            definition.OnSellReward);
    }

    private static EntityAttributes<TIdentity> Copy<TIdentity>(EntityAttributes<TIdentity> source)
        where TIdentity : class =>
        new(source.Identity, source.Persistent.CreateMutableCopy(), source.BaseCombat.CreateMutableCopy());
}
