using Project_Star.Domain.Match;

namespace Project_Star.Application.Encounters;

public interface IOpponentProvider
{
    MatchSession CreateOpponent(ulong seed);
}
