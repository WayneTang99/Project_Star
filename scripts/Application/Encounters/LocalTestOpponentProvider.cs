using System;
using System.Linq;
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
        var hero = _registry.Heroes.Values
            .OrderBy(definition => definition.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("At least one hero definition is required to create an opponent.");
        var cardDefinition = _registry.Cards.Values
            .OrderBy(definition => definition.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("At least one card definition is required to create an opponent.");
        var session = new CreateMatchService(_factory).Create(
            seed, 0, hero);
        var card = new CardEconomyService(_factory).AcquireCard(
            session,
            cardDefinition,
            cardDefinition.InitialLevel,
            CardAcquisitionSource.Reward).Value!;
        _ = new BoardService(new BoardPlacementSolver()).PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        return session;
    }
}
