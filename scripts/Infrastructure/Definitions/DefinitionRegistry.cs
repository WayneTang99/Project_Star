using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Infrastructure.Definitions;

/// <summary>Discovers immutable content definitions and validates their cross-references.</summary>
public sealed class DefinitionRegistry
{
    private DefinitionRegistry(
        Dictionary<StringName, HeroDefinition> heroes,
        Dictionary<StringName, CardDefinition> cards,
        Dictionary<StringName, EncounterDefinition> encounters)
    {
        Heroes = heroes;
        Cards = cards;
        Encounters = encounters;
    }

    public IReadOnlyDictionary<StringName, HeroDefinition> Heroes { get; }

    public IReadOnlyDictionary<StringName, CardDefinition> Cards { get; }

    public IReadOnlyDictionary<StringName, EncounterDefinition> Encounters { get; }

    public static DefinitionRegistry Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var definitions = new List<object>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsPublic || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (!IsDefinitionType(type))
            {
                continue;
            }

            try
            {
                definitions.Add(Activator.CreateInstance(type)!);
            }
            catch (Exception exception)
            {
                throw new DefinitionValidationException($"Could not create definition '{type.FullName}'.", exception);
            }
        }

        return Create(definitions);
    }

    public static DefinitionRegistry Create(IEnumerable<object> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var heroes = new Dictionary<StringName, HeroDefinition>();
        var cards = new Dictionary<StringName, CardDefinition>();
        var encounters = new Dictionary<StringName, EncounterDefinition>();

        foreach (var definition in definitions)
        {
            switch (definition)
            {
                case HeroDefinition hero:
                    AddUnique(heroes, hero.Attributes.Identity.Key, hero, "hero");
                    break;
                case CardDefinition card:
                    AddUnique(cards, card.Attributes.Identity.Key, card, "card");
                    break;
                case EncounterDefinition encounter:
                    ValidateEncounter(encounter);
                    AddUnique(encounters, encounter.Attributes.Identity.Key, encounter, "encounter");
                    break;
                default:
                    throw new DefinitionValidationException($"Unsupported definition type '{definition?.GetType().FullName ?? "null"}'.");
            }
        }

        ValidateCardFactions(heroes, cards);
        return new DefinitionRegistry(heroes, cards, encounters);
    }

    private static bool IsDefinitionType(Type type) =>
        typeof(HeroDefinition).IsAssignableFrom(type)
        || typeof(CardDefinition).IsAssignableFrom(type)
        || typeof(EncounterDefinition).IsAssignableFrom(type);

    private static void AddUnique<T>(Dictionary<StringName, T> target, StringName key, T value, string category)
    {
        if (!target.TryAdd(key, value))
        {
            throw new DefinitionValidationException($"Duplicate {category} key '{key}'.");
        }
    }

    private static void ValidateCardFactions(
        IReadOnlyDictionary<StringName, HeroDefinition> heroes,
        IReadOnlyDictionary<StringName, CardDefinition> cards)
    {
        var factions = new HashSet<StringName>();
        foreach (var hero in heroes.Values)
        {
            factions.Add(hero.Attributes.Identity.FactionKey);
        }

        foreach (var card in cards.Values)
        {
            if (card.Attributes.Identity.FactionKey != GameFactions.Neutral
                && !factions.Contains(card.Attributes.Identity.FactionKey))
            {
                throw new DefinitionValidationException(
                    $"Card '{card.Attributes.Identity.Key}' references unknown faction '{card.Attributes.Identity.FactionKey}'.");
            }
        }
    }

    private static void ValidateEncounter(EncounterDefinition encounter)
    {
        if (encounter.MinimumRound < 1 || encounter.MaximumRound < encounter.MinimumRound
            || encounter.BaseWeight < 1 || !Enum.IsDefined(encounter.Kind))
        {
            throw new DefinitionValidationException(
                $"Encounter '{encounter.Attributes.Identity.Key}' has invalid round range "
                + $"{encounter.MinimumRound}..{encounter.MaximumRound}.");
        }
    }
}

public sealed class DefinitionValidationException : Exception
{
    public DefinitionValidationException(string message) : base(message)
    {
    }

    public DefinitionValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
