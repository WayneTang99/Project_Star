using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
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
    IReadOnlyList<QuestProgressSnapshot> Quests);

public sealed record BoardPlacementSnapshot(EntityId CardId, BoardZone Zone, int Start, int EndExclusive);

public sealed record SkillSnapshot(
    EntityId Id,
    StringName Key,
    string DisplayName,
    int Level,
    StringName FactionKey);

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
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value),
                identity.Size,
                identity.FactionKey,
                identity.ElementKeys,
                quests.AsReadOnly()));
        }
        var placements = new List<BoardPlacementSnapshot>();
        var skills = new List<SkillSnapshot>();
        foreach (var skill in session.Player.Skills.Items)
            skills.Add(new SkillSnapshot(
                skill.Id,
                skill.Attributes.Identity.Key,
                skill.Attributes.Identity.DisplayName,
                skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                skill.Attributes.Identity.FactionKey));
        AddPlacements(session.Board.Battlefield, BoardZone.Battlefield, placements);
        AddPlacements(session.Board.Bench, BoardZone.Bench, placements);
        return new MatchSnapshot(session.Id, session.Status, session.Progress.Round, session.Progress.Turn,
            session.Player.Wealth, session.Player.Experience, session.Player.Income, session.Player.Reputation, session.Progress.PvpWins,
            cards.AsReadOnly(), skills.AsReadOnly(), Array.AsReadOnly(session.PendingMonsterRewards.ToArray()),
            Array.AsReadOnly(session.EncounterSchedule.CurrentChoices.ToArray()), placements.AsReadOnly(),
            session.Board.Battlefield.Count, session.Board.Bench.Count, session.Summary);
    }

    private static void AddPlacements(
        BoardZoneState zoneState,
        BoardZone zone,
        ICollection<BoardPlacementSnapshot> placements)
    {
        foreach (var placement in zoneState.Placements)
            placements.Add(new BoardPlacementSnapshot(placement.CardId, zone, placement.Start, placement.EndExclusive));
    }
}
