using System;
using Project_Star.Application.Factories;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

public sealed class CreateMatchService
{
    private readonly EntityFactory _entityFactory;

    public CreateMatchService(EntityFactory entityFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
    }

    public MatchSession Create(ulong seed, int startingWealth, HeroDefinition heroDefinition)
    {
        ArgumentNullException.ThrowIfNull(heroDefinition);
        var session = new MatchSession(seed, startingWealth);
        session.Player.SelectHero(_entityFactory.CreateHero(heroDefinition));
        _ = new RoundIncomeService().SettleCurrentRound(session);
        return session;
    }
}
