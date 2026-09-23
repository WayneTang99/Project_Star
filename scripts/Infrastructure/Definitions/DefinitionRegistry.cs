using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Infrastructure.Definitions;

/// <summary>Discovers immutable content definitions and validates their cross-references.</summary>
public sealed class DefinitionRegistry
{
    private DefinitionRegistry(
        Dictionary<StringName, HeroDefinition> heroes,
        Dictionary<StringName, CardDefinition> cards,
        Dictionary<StringName, SkillDefinition> skills,
        Dictionary<StringName, EncounterDefinition> encounters,
        Dictionary<StringName, MonsterDefinition> monsters)
    {
        Heroes = heroes;
        Cards = cards;
        Skills = skills;
        Encounters = encounters;
        Monsters = monsters;
    }

    public IReadOnlyDictionary<StringName, HeroDefinition> Heroes { get; }

    public IReadOnlyDictionary<StringName, CardDefinition> Cards { get; }

    public IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }

    public IReadOnlyDictionary<StringName, EncounterDefinition> Encounters { get; }

    public IReadOnlyDictionary<StringName, MonsterDefinition> Monsters { get; }

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
        var skills = new Dictionary<StringName, SkillDefinition>();
        var encounters = new Dictionary<StringName, EncounterDefinition>();
        var monsters = new Dictionary<StringName, MonsterDefinition>();

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
                case SkillDefinition skill:
                    AddUnique(skills, skill.Attributes.Identity.Key, skill, "skill");
                    break;
                case EncounterDefinition encounter:
                    ValidateEncounter(encounter);
                    AddUnique(encounters, encounter.Attributes.Identity.Key, encounter, "encounter");
                    break;
                case MonsterDefinition monster:
                    AddUnique(monsters, monster.Attributes.Identity.Key, monster, "monster");
                    break;
                default:
                    throw new DefinitionValidationException($"Unsupported definition type '{definition?.GetType().FullName ?? "null"}'.");
            }
        }

        ValidateCardFactions(heroes, cards);
        ValidateMonsterCards(monsters, cards);
        return new DefinitionRegistry(heroes, cards, skills, encounters, monsters);
    }

    private static bool IsDefinitionType(Type type) =>
        typeof(HeroDefinition).IsAssignableFrom(type)
        || typeof(CardDefinition).IsAssignableFrom(type)
        || typeof(SkillDefinition).IsAssignableFrom(type)
        || typeof(EncounterDefinition).IsAssignableFrom(type)
        || typeof(MonsterDefinition).IsAssignableFrom(type);

    private static void ValidateMonsterCards(
        IReadOnlyDictionary<StringName, MonsterDefinition> monsters,
        IReadOnlyDictionary<StringName, CardDefinition> cards)
    {
        foreach (var monster in monsters.Values)
        {
            var occupied = new bool[BoardState.DefaultCapacity];
            foreach (var entry in monster.Cards)
            {
                if (!cards.TryGetValue(entry.CardKey, out var card))
                    throw new DefinitionValidationException(
                        $"Monster '{monster.Attributes.Identity.Key}' references unknown card '{entry.CardKey}'.");
                if (!card.SupportsLevel(entry.Level))
                    throw new DefinitionValidationException(
                        $"Monster '{monster.Attributes.Identity.Key}' references unsupported level {entry.Level} "
                        + $"for card '{entry.CardKey}'.");
                if (entry.BoardStart < 0 || entry.BoardStart + card.Attributes.Identity.OccupiedSlots > BoardState.DefaultCapacity)
                    throw new DefinitionValidationException(
                        $"Monster '{monster.Attributes.Identity.Key}' has an invalid board position for card '{entry.CardKey}'.");
                for (var slot = entry.BoardStart; slot < entry.BoardStart + card.Attributes.Identity.OccupiedSlots; slot++)
                {
                    if (occupied[slot])
                        throw new DefinitionValidationException(
                            $"Monster '{monster.Attributes.Identity.Key}' has overlapping cards at board slot {slot}.");
                    occupied[slot] = true;
                }
            }
        }
    }

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
        if (encounter is not ChoiceEncounterDefinition choiceEncounter) return;
        var optionKeys = new HashSet<StringName>();
        foreach (var option in choiceEncounter.Options)
        {
            if (!optionKeys.Add(option.Key))
                throw new DefinitionValidationException(
                    $"Encounter '{encounter.Attributes.Identity.Key}' has duplicate option '{option.Key}'.");
            foreach (var effect in option.Effects)
            {
                var invalid = effect switch
                {
                    ModifyHeroCombatAttributeByLevelEffectDefinition modifier =>
                        modifier.AttributeKey.IsEmpty || modifier.AmountPerLevel < 1,
                    GainWealthEncounterOptionEffectDefinition gain => gain.Amount < 1,
                    GrantRandomFactionCardEncounterOptionEffectDefinition reward =>
                        !Enum.IsDefined(reward.Size) || reward.Level is < 1 or > 5,
                    GrantRandomTaggedCardEncounterOptionEffectDefinition reward =>
                        reward.RequiredTag.IsEmpty || !Enum.IsDefined(reward.Size) || reward.Level is < 1 or > 5,
                    _ => true,
                };
                if (invalid)
                {
                    throw new DefinitionValidationException(
                        $"Encounter option '{option.Key}' has an invalid effect.");
                }
            }
        }
        var slottedKeys = new HashSet<StringName>();
        foreach (var slot in choiceEncounter.OptionSlots)
        foreach (var candidate in slot.Candidates)
        {
            if (candidate.Weight < 1 || !optionKeys.Contains(candidate.OptionKey) || !slottedKeys.Add(candidate.OptionKey))
            {
                throw new DefinitionValidationException(
                    $"Encounter '{encounter.Attributes.Identity.Key}' has an invalid option slot candidate.");
            }
        }
        if (slottedKeys.Count != optionKeys.Count)
        {
            throw new DefinitionValidationException(
                $"Encounter '{encounter.Attributes.Identity.Key}' has an option that is not assigned to a slot.");
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
