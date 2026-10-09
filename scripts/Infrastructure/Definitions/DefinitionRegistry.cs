using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;

namespace Project_Star.Infrastructure.Definitions;

/// <summary>Discovers immutable content definitions and validates their cross-references.</summary>
public sealed class DefinitionRegistry : IDefinitionCatalog
{
    private DefinitionRegistry(
        Dictionary<StringName, HeroDefinition> heroes,
        Dictionary<StringName, CardDefinition> cards,
        Dictionary<StringName, CardSetDefinition> sets,
        Dictionary<StringName, SkillDefinition> skills,
        Dictionary<StringName, EncounterDefinition> encounters,
        Dictionary<StringName, MonsterDefinition> monsters,
        Dictionary<StringName, GemDefinition> gems,
        Dictionary<StringName, MentorDefinition> mentors)
    {
        Heroes = heroes;
        Cards = cards;
        Sets = sets;
        Skills = skills;
        Encounters = encounters;
        Monsters = monsters;
        Gems = gems;
        Mentors = mentors;
    }

    public IReadOnlyDictionary<StringName, HeroDefinition> Heroes { get; }

    public IReadOnlyDictionary<StringName, CardDefinition> Cards { get; }

    public IReadOnlyDictionary<StringName, CardSetDefinition> Sets { get; }

    public IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }

    public IReadOnlyDictionary<StringName, EncounterDefinition> Encounters { get; }

    public IReadOnlyDictionary<StringName, MonsterDefinition> Monsters { get; }
    public IReadOnlyDictionary<StringName, GemDefinition> Gems { get; }
    public IReadOnlyDictionary<StringName, MentorDefinition> Mentors { get; }

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
        var sets = new Dictionary<StringName, CardSetDefinition>();
        var skills = new Dictionary<StringName, SkillDefinition>();
        var encounters = new Dictionary<StringName, EncounterDefinition>();
        var monsters = new Dictionary<StringName, MonsterDefinition>();
        var gems = new Dictionary<StringName, GemDefinition>();
        var mentors = new Dictionary<StringName, MentorDefinition>();

        foreach (var definition in definitions)
        {
            switch (definition)
            {
                case MentorDefinition mentor:
                    AddUnique(mentors, mentor.Attributes.Identity.Key, mentor, "mentor");
                    break;
                case GemDefinition gem:
                    AddUnique(gems, gem.Attributes.Identity.Key, gem, "gem");
                    break;
                case HeroDefinition hero:
                    AddUnique(heroes, hero.Attributes.Identity.Key, hero, "hero");
                    break;
                case CardDefinition card:
                    AddUnique(cards, card.Attributes.Identity.Key, card, "card");
                    break;
                case CardSetDefinition set:
                    AddUnique(sets, set.Attributes.Identity.Key, set, "set");
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

        ValidateContentFactions(heroes, cards, skills);
        ValidateCardSets(cards, sets);
        ValidateTransformations(cards);
        ValidateSummons(cards);
        ValidateMonsterCards(monsters, cards);
        ValidateMonsterSkills(monsters, skills);
        foreach (var encounter in encounters.Values)
            if (encounter is MentorEncounterDefinition mentorEncounter && !mentors.ContainsKey(mentorEncounter.MentorKey))
                throw new DefinitionValidationException(
                    $"Encounter '{encounter.Attributes.Identity.Key}' references unknown mentor '{mentorEncounter.MentorKey}'.");
        return new DefinitionRegistry(heroes, cards, sets, skills, encounters, monsters, gems, mentors);
    }

    private static bool IsDefinitionType(Type type) =>
        typeof(HeroDefinition).IsAssignableFrom(type)
        || typeof(CardDefinition).IsAssignableFrom(type)
        || typeof(CardSetDefinition).IsAssignableFrom(type)
        || typeof(SkillDefinition).IsAssignableFrom(type)
        || typeof(EncounterDefinition).IsAssignableFrom(type)
        || typeof(MonsterDefinition).IsAssignableFrom(type)
        || typeof(MentorDefinition).IsAssignableFrom(type)
        || typeof(GemDefinition).IsAssignableFrom(type);

    // 校验转变使用的替换卡定义存在于正式目录。
    private static void ValidateTransformations(IReadOnlyDictionary<StringName, CardDefinition> cards)
    {
        foreach (var card in cards.Values)
        {
            var abilities = new List<AbilityDefinition>(card.Abilities);
            foreach (var level in card.Levels.Values) abilities.AddRange(level.Abilities);
            foreach (var ability in abilities)
            foreach (var effect in ability.Effects)
                if (effect is TransformRandomEnemyCardEffectDefinition transform
                    && !cards.ContainsKey(transform.Replacement.Attributes.Identity.Key))
                    throw new DefinitionValidationException(
                        $"Card '{card.Attributes.Identity.Key}' references unknown transformation '{transform.Replacement.Attributes.Identity.Key}'.");
        }
    }

    // 召唤模板必须已注册，并支持来源的等级。
    private static void ValidateSummons(IReadOnlyDictionary<StringName, CardDefinition> cards)
    {
        foreach (var card in cards.Values)
        {
            Validate(card.Abilities, card.InitialLevel);
            foreach (var level in card.Levels.Values) Validate(level.Abilities, level.Level);
        }
        void Validate(IReadOnlyList<AbilityDefinition> abilities, int level)
        {
            foreach (var ability in abilities)
            foreach (var effect in ability.Effects)
            foreach (var template in effect switch
                {
                    SummonAdjacentCardEffectDefinition summon => new[] { summon.Card },
                    SummonRandomAdjacentCardEffectDefinition summon => summon.Cards,
                    _ => System.Array.Empty<CardDefinition>(),
                })
                if (!cards.ContainsKey(template.Attributes.Identity.Key) || !template.SupportsLevel(level))
                    throw new DefinitionValidationException(
                        $"Summon references unknown card or unsupported level {level}: '{template.Attributes.Identity.Key}'.");
        }
    }

    private static void ValidateCardSets(
        IReadOnlyDictionary<StringName, CardDefinition> cards,
        IReadOnlyDictionary<StringName, CardSetDefinition> sets)
    {
        foreach (var card in cards.Values)
        {
            var setKey = card.Attributes.Identity.SetKey;
            if (setKey is not null && !sets.ContainsKey(setKey))
                throw new DefinitionValidationException(
                    $"Card '{card.Attributes.Identity.Key}' references unknown set '{setKey}'.");
        }
    }

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

    private static void ValidateMonsterSkills(
        IReadOnlyDictionary<StringName, MonsterDefinition> monsters,
        IReadOnlyDictionary<StringName, SkillDefinition> skills)
    {
        foreach (var monster in monsters.Values)
        foreach (var entry in monster.Skills)
        {
            if (!skills.TryGetValue(entry.SkillKey, out var skill)
                || !skill.SupportsLevel(entry.Level))
                throw new DefinitionValidationException(
                    $"Monster '{monster.Attributes.Identity.Key}' references an invalid skill '{entry.SkillKey}' at level {entry.Level}.");
        }
    }

    private static void AddUnique<T>(Dictionary<StringName, T> target, StringName key, T value, string category)
    {
        if (!target.TryAdd(key, value))
        {
            throw new DefinitionValidationException($"Duplicate {category} key '{key}'.");
        }
    }

    private static void ValidateContentFactions(
        IReadOnlyDictionary<StringName, HeroDefinition> heroes,
        IReadOnlyDictionary<StringName, CardDefinition> cards,
        IReadOnlyDictionary<StringName, SkillDefinition> skills)
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
        foreach (var skill in skills.Values)
        {
            if (skill.Attributes.Identity.FactionKey != GameFactions.Neutral
                && !factions.Contains(skill.Attributes.Identity.FactionKey))
            {
                throw new DefinitionValidationException(
                    $"Skill '{skill.Attributes.Identity.Key}' references unknown faction "
                    + $"'{skill.Attributes.Identity.FactionKey}'.");
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
                if (InvalidEncounterEffect(effect))
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

    private static bool InvalidEncounterEffect(EncounterOptionEffectDefinition effect) => effect switch
    {
        ModifyHeroCombatAttributeByLevelEffectDefinition modifier => modifier.AttributeKey.IsEmpty || modifier.AmountPerLevel < 1,
        GainWealthEncounterOptionEffectDefinition gain => gain.Amount < 1,
        GrantRandomFactionCardEncounterOptionEffectDefinition reward => !Enum.IsDefined(reward.Size) || reward.Level is < 1 or > 5,
        GrantRandomTaggedCardEncounterOptionEffectDefinition reward =>
            reward.RequiredTag.IsEmpty || !Enum.IsDefined(reward.Size) || reward.Level is < 1 or > 5,
        BuyRandomOtherFactionCardEncounterOptionEffectDefinition purchase =>
            !Enum.IsDefined(purchase.Size) || purchase.Level is < 1 or > 5 || purchase.Cost < 1,
        GrantNextBattleMaxHealthByLevelEncounterOptionEffectDefinition battleHealth => battleHealth.AmountPerLevel < 1,
        ModifyBattlefieldCardAttributeEncounterOptionEffectDefinition modifier => modifier.AttributeKey.IsEmpty || modifier.Amount == 0,
        EnterRandomMonsterEncounterOptionEffectDefinition => false,
        WeightedEncounterOptionEffectDefinition weighted => InvalidWeightedEffect(weighted),
        _ => true,
    };

    private static bool InvalidWeightedEffect(WeightedEncounterOptionEffectDefinition weighted)
    {
        long total = 0;
        foreach (var outcome in weighted.Outcomes)
        {
            if (outcome is null || outcome.Weight < 1 || InvalidEncounterEffect(outcome.Effect)) return true;
            total += outcome.Weight;
        }
        return total > int.MaxValue;
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
