using System;
using Project_Star.Application.Factories;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

public sealed class CreateMatchService
{
    private readonly EntityFactory _entityFactory;
    private readonly IDefinitionCatalog? _definitions;
    private readonly System.Collections.Generic.IEnumerable<GemDefinition>? _gems;

    public CreateMatchService(EntityFactory entityFactory, System.Collections.Generic.IEnumerable<GemDefinition>? gems = null, IDefinitionCatalog? definitions = null)
    {
        _definitions = definitions;
        _gems = gems;
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
    }

    public MatchSession Create(ulong seed, int startingWealth, HeroDefinition heroDefinition)
    {
        ArgumentNullException.ThrowIfNull(heroDefinition);
        var session = new MatchSession(seed, startingWealth, gems: _gems, definitions: _definitions);
        session.Player.SelectHero(_entityFactory.CreateHero(heroDefinition));
        _ = new RoundIncomeService().SettleCurrentRound(session);
        _ = new TurnExperienceService().SettleCurrentTurn(session);
        return session;
    }
}
