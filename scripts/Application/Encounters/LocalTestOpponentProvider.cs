using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Application.Encounters;

public sealed class LocalTestOpponentProvider : IOpponentProvider
{
    private readonly DefinitionRegistry _registry;
    private readonly EntityFactory _factory = new();

    public LocalTestOpponentProvider(DefinitionRegistry registry) => _registry = registry;

    public MatchSession CreateOpponent(ulong seed)
    {
        var session = new CreateMatchService(_factory).Create(
            seed, 0, _registry.Heroes[new StringName("hero.wayfarer")]);
        var card = new CardEconomyService(_factory).AcquireCard(
            session,
            _registry.Cards[new StringName("card.iron_sword")],
            1,
            CardAcquisitionSource.Reward).Value!;
        _ = new BoardService(new BoardPlacementSolver()).PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        return session;
    }
}
