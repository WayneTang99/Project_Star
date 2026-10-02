using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 单张卡牌的只读任务进度（应用快照层）。
public sealed record QuestProgressSnapshot(StringName Key, int Progress, int RequiredCount, bool Unlocked);

public sealed record CardSnapshot(
    EntityId Id,
    StringName Key,
    string DisplayName,
    int Level,
    int Value,
    CardSize Size,
    StringName FactionKey,
    IReadOnlyList<StringName> ElementKeys,
    IReadOnlyList<QuestProgressSnapshot> Quests)
{
    public StringName? SetKey { get; init; }
    public StringName Illustration { get; init; } = new("");
    public IReadOnlyList<CardDescriptionEntry> DescriptionEntries { get; init; } = Array.Empty<CardDescriptionEntry>();
    public IReadOnlyList<StringName> Tags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyDictionary<StringName, int> BaseValues { get; init; } =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    public IReadOnlyDictionary<StringName, int> CurrentValues { get; init; } =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = Array.Empty<AbilityDefinition>();
}

public sealed record HeroSnapshot(EntityId Id, StringName Key, string DisplayName,
    StringName FactionKey, int Level, IReadOnlyDictionary<StringName, int> CombatValues)
{
    public string Title { get; init; } = "";
}

public sealed record BoardPlacementSnapshot(EntityId CardId, BoardZone Zone, int Start, int EndExclusive);

public sealed record SkillSnapshot(
    EntityId Id,
    StringName Key,
    string DisplayName,
    int Level,
    StringName FactionKey)
{
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = Array.Empty<AbilityDefinition>();
    public IReadOnlyDictionary<StringName, int> CurrentValues { get; init; } =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
}

public sealed record CardSetThresholdSnapshot(int RequiredCount, bool Active, IReadOnlyList<AbilityDefinition> Abilities);
public sealed record CardSetSnapshot(StringName Key, string DisplayName, int DistinctCardCount,
    IReadOnlyList<CardSetThresholdSnapshot> Thresholds);

public sealed record MatchSnapshot(
    Guid MatchId,
    MatchStatus Status,
    int Round,
    int Turn,
    int Wealth,
    int Experience,
    int Income,
    int Reputation,
    int PvpWins,
    IReadOnlyList<CardSnapshot> Cards,
    IReadOnlyList<SkillSnapshot> Skills,
    IReadOnlyList<PendingMonsterReward> PendingMonsterRewards,
    IReadOnlyList<EncounterChoice> EncounterChoices,
    IReadOnlyList<BoardPlacementSnapshot> BoardPlacements,
    int BattlefieldCount,
    int BenchCount,
    MatchSummary? Summary)
{
    public HeroSnapshot? Hero { get; init; }
    public int BattlefieldCapacity { get; init; }
    public int BenchCapacity { get; init; }
    public IReadOnlyList<CardSetSnapshot> Sets { get; init; } = Array.Empty<CardSetSnapshot>();

    public static MatchSnapshot From(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var cards = new List<CardSnapshot>();
        foreach (var card in session.Player.Inventory.Cards)
        {
            var identity = card.Attributes.Identity;
            var quests = new List<QuestProgressSnapshot>();
            foreach (var quest in card.Quests)
                quests.Add(new QuestProgressSnapshot(
                    quest.Key, card.GetQuestProgress(quest.Key), quest.RequiredCount, card.IsQuestUnlocked(quest)));
            cards.Add(new CardSnapshot(
                card.Id,
                identity.Key,
                identity.DisplayName,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value),
                identity.Size,
                identity.FactionKey,
                Array.AsReadOnly(identity.ElementKeys.ToArray()),
                quests.AsReadOnly())
            {
                SetKey = identity.SetKey,
                Illustration = identity.Illustration,
                DescriptionEntries = Array.AsReadOnly(identity.DescriptionEntries.ToArray()),
                Tags = Array.AsReadOnly(card.Tags.OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray()),
                BaseValues = card.Attributes.BaseCombat.CreateMutableCopy().SnapshotFinalValues(),
                CurrentValues = card.Attributes.BaseCombat.SnapshotFinalValues(),
                Abilities = CopyAbilities(card.Abilities.Concat(card.Quests
                    .Where(card.IsQuestUnlocked).SelectMany(quest => quest.Abilities))),
            });
        }
        var placements = new List<BoardPlacementSnapshot>();
        var skills = new List<SkillSnapshot>();
        foreach (var skill in session.Player.Skills.Items)
            skills.Add(new SkillSnapshot(
                skill.Id,
                skill.Attributes.Identity.Key,
                skill.Attributes.Identity.DisplayName,
                skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                skill.Attributes.Identity.FactionKey)
            {
                Abilities = CopyAbilities(skill.Abilities),
                CurrentValues = skill.Attributes.BaseCombat.SnapshotFinalValues(),
            });
        AddPlacements(session.Board.Battlefield, BoardZone.Battlefield, placements);
        AddPlacements(session.Board.Bench, BoardZone.Bench, placements);
        return new MatchSnapshot(session.Id, session.Status, session.Progress.Round, session.Progress.Turn,
            session.Player.Wealth, session.Player.Experience, session.Player.Income, session.Player.Reputation, session.Progress.PvpWins,
            cards.AsReadOnly(), skills.AsReadOnly(), Array.AsReadOnly(session.PendingMonsterRewards.ToArray()),
            Array.AsReadOnly(session.EncounterSchedule.CurrentChoices.ToArray()), placements.AsReadOnly(),
            session.Board.Battlefield.Count, session.Board.Bench.Count, session.Summary)
        {
            Hero = session.Player.Hero is not { } hero ? null : new HeroSnapshot(
                hero.Id, hero.Attributes.Identity.Key, hero.Attributes.Identity.DisplayName,
                hero.Attributes.Identity.FactionKey,
                hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level),
                hero.Attributes.BaseCombat.SnapshotFinalValues()) { Title = hero.Attributes.Identity.Title },
            BattlefieldCapacity = session.Board.Battlefield.Capacity,
            BenchCapacity = session.Board.Bench.Capacity,
        };
    }

    // Copy collection-bearing effects so a captured view cannot change with later content edits.
    internal static IReadOnlyList<AbilityDefinition> CopyAbilities(IEnumerable<AbilityDefinition> abilities) =>
        Array.AsReadOnly(abilities.Select(ability => new AbilityDefinition(
            ability.Key, ability.Activation, ability.Target, ability.ManaCost, ability.CooldownTicks,
            Array.AsReadOnly(ability.Effects.Select(effect => effect switch
            {
                DestroyRandomEnemyCardEffectDefinition value => value with
                {
                    RequiredAnyTags = Array.AsReadOnly(value.RequiredAnyTags.ToArray()),
                    AllowedSizes = Array.AsReadOnly(value.AllowedSizes.ToArray()),
                },
                MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition value => value with
                { RequiredAnyTags = Array.AsReadOnly(value.RequiredAnyTags.ToArray()) },
                _ => effect,
            }).ToArray()), ability.AllowsBench)).ToArray());

    private static void AddPlacements(
        BoardZoneState zoneState,
        BoardZone zone,
        ICollection<BoardPlacementSnapshot> placements)
    {
        foreach (var placement in zoneState.Placements)
            placements.Add(new BoardPlacementSnapshot(placement.CardId, zone, placement.Start, placement.EndExclusive));
    }
}
