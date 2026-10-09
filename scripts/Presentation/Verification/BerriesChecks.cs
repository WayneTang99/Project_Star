using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.Verification;

// 浆果出售的等级、来源区域、永久英雄贡献与交易失败验证（表现层验证模块）。
internal static class BerriesChecks
{
    internal static bool LevelsAndSales()
    {
        var definition = new BerriesCardDefinition();
        if (definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != GameFactions.Neutral
            || definition.Attributes.Identity.Size != CardSize.Small
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Small) || !definition.Tags.Contains(GameTags.Plant))
            return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var factory = new EntityFactory();
            var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new PaladinHeroDefinition());
            var opponent = matches.Create(2, 50, new PaladinHeroDefinition());
            var card = economy.AcquireCard(session, definition, level, CardAcquisitionSource.Reward).Value!.Card;
            if (zone is { } placed) _ = board.PlaceCard(session, card.Id, placed, 0);
            var before = MaxHealth(session);
            var wealth = session.Player.Wealth;
            var value = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = session.Random.State;
            var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(1));
            if (card.Abilities.Count != 0
                || card.OnSellReward is not IncreaseHeroCombatAttributeOnSellDefinition bonus
                || bonus.AttributeKey != GameAttributeKeys.MaxHealth || bonus.Amount != level * 5) return false;
            var sold = zone is null ? economy.SellCard(session, card.Id) : economy.SellFromBoard(session, card.Id);
            if (sold.IsFailure || sold.Value != value || session.Player.Wealth != wealth + value
                || MaxHealth(session) != before + level * 5 || session.Random.State != random
                || session.Player.Inventory.Find(card.Id) is not null || session.Board.Contains(card.Id)
                || economy.SellFromBoard(session, card.Id).IsSuccess || MaxHealth(session) != before + level * 5
                || frozen.Player.Hero.MaxHealth != before
                || new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(1)).Player.Hero.MaxHealth != before + level * 5)
                return false;
        }
        return true;
    }

    internal static bool StackingMergeAndFailures()
    {
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new CreateMatchService(factory).Create(1, 50, new PaladinHeroDefinition());
        var definition = new BerriesCardDefinition();
        var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        _ = board.PlaceCard(session, first.Id, BoardZone.Bench, 0);
        if (economy.SellFromBoard(session, first.Id).IsFailure) return false;
        var second = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        _ = board.PlaceCard(session, second.Id, BoardZone.Battlefield, 0);
        var upgraded = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
        if (upgraded.Id != second.Id || upgraded.CurrentLevel != 2 || MaxHealth(session) != 105
            || economy.SellFromBoard(session, second.Id).IsFailure || MaxHealth(session) != 115
            || session.Player.Hero!.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) != 100) return false;
        var final = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        _ = board.PlaceCard(session, final.Id, BoardZone.Battlefield, 0);
        var wealth = session.Player.Wealth;
        var random = session.Random.State;
        // 在棋盘上的卡不能从游离库存出售入口绕过区域检查。
        if (economy.SellCard(session, final.Id).IsSuccess || MaxHealth(session) != 115) return false;
        session.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, int.MaxValue - 19);
        if (economy.SellFromBoard(session, final.Id).IsSuccess || !session.Board.Contains(final.Id)
            || session.Player.Inventory.Find(final.Id) is null || session.Player.Wealth != wealth
            || MaxHealth(session) != int.MaxValue - 4 || session.Random.State != random) return false;
        // 边界允许恰好达到int上限，既有15点贡献继续保留。
        session.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, int.MaxValue - 20);
        if (economy.SellFromBoard(session, final.Id).IsFailure || MaxHealth(session) != int.MaxValue) return false;
        var empty = new MatchSession(7);
        var unownedHeroCard = factory.CreateCard(definition);
        empty.Player.Inventory.Add(unownedHeroCard);
        _ = board.PlaceCard(empty, unownedHeroCard.Id, BoardZone.Bench, 0);
        return economy.SellFromBoard(empty, unownedHeroCard.Id).IsFailure
            && empty.Player.Inventory.Find(unownedHeroCard.Id) is not null && empty.Board.Contains(unownedHeroCard.Id)
            && empty.Player.Wealth == 0;
    }

    private static int MaxHealth(MatchSession session) => session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
}
