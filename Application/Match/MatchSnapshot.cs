using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

public sealed record CardSnapshot(EntityId Id, string DisplayName, int Value);

public sealed record MatchSnapshot(
    Guid MatchId,
    MatchStatus Status,
    int Round,
    int Turn,
    int Wealth,
    int Reputation,
    int PvpWins,
    IReadOnlyList<CardSnapshot> Cards,
    MatchSummary? Summary)
{
    public static MatchSnapshot From(MatchSession session)
    {
        var cards = new List<CardSnapshot>();
        foreach (var card in session.Player.Inventory.Cards)
            cards.Add(new CardSnapshot(card.Id, card.Attributes.Identity.DisplayName,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value)));
        return new MatchSnapshot(session.Id, session.Status, session.Progress.Round, session.Progress.Turn,
            session.Player.Wealth, session.Player.Reputation, session.Progress.PvpWins, cards.AsReadOnly(), session.Summary);
    }
}
