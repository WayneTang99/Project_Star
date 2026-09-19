namespace Project_Star.Domain.Match;

public enum MatchStatus
{
    InProgress = 0,
    Won = 1,
    Lost = 2,
}

public sealed record MatchSummary(
    MatchStatus Status,
    int CompletedRounds,
    int PvpWins,
    int FinalReputation,
    int FinalWealth);
