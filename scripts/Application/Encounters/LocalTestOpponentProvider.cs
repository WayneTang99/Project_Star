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

// 使用正式内容注册表创建本地试玩对手（应用层）。
public sealed class LocalTestOpponentProvider : IOpponentProvider
{
    private readonly DefinitionRegistry _registry;
    private readonly EntityFactory _factory = new();

    public LocalTestOpponentProvider(DefinitionRegistry registry) => _registry = registry;

    // 创建本地 PvP 测试对手。
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
        _ = new BoardService(new BoardPlacementSolver(), _registry.Sets)
            .PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        return session;
    }

    public MatchSession CreateOpponent(MatchSession player) => CreateOpponent(player.Random.State);

    // 按怪物固定属性与卡组创建独立对手实例。
    public MatchSession CreateMonsterOpponent(ulong seed, MonsterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var session = new MatchSession(seed);
        session.Player.SelectHero(_factory.CreateMonsterCombatant(definition));
        var board = new BoardService(new BoardPlacementSolver(), _registry.Sets);
        foreach (var entry in definition.Cards)
        {
            var cardDefinition = _registry.Cards[entry.CardKey];
            var card = _factory.CreateCard(cardDefinition, entry.Level);
            session.Player.Inventory.Add(card);
            var placement = board.PlaceCard(session, card.Id, BoardZone.Battlefield, entry.BoardStart);
            if (placement.IsFailure)
                throw new InvalidOperationException(placement.Failure!.Message);
        }
        foreach (var entry in definition.Skills)
            session.Player.Skills.Add(_factory.CreateSkill(_registry.Skills[entry.SkillKey], entry.Level));
        return session;
    }

    public MatchSession CreateMonsterOpponent(MatchSession player, MonsterDefinition definition) =>
        CreateMonsterOpponent(player.Random.State, definition);
}
