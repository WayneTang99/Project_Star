using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Server;

namespace Project_Star.Presentation.Verification;

// 联网构筑操作的规则与存档验证，夹具不进入正式对局（表现层验证模块）。
internal static class ServerConstructionChecks
{
    internal static void Run(IDefinitionCatalog definitions, MatchSaveCodec codec, ServerMatchService service, string afterBuy)
    {
        var run = codec.Decode(afterBuy);
        var owned = run.Match.Player.Inventory.Cards.Single();
        var before = codec.Encode(run);
        Require(service.MoveCard(run, new EntityId(Guid.NewGuid()), BoardZone.Bench, 0).IsFailure
            && service.SellCard(run, new EntityId(Guid.NewGuid())).IsFailure && codec.Encode(run) == before,
            "construction rejects unknown card without mutation");
        Require(service.MoveCard(run, owned.Id, (BoardZone)99, 0).IsFailure
            && service.MoveCard(run, owned.Id, BoardZone.Bench, 10).IsFailure && codec.Encode(run) == before,
            "invalid board zone and outside position do not remove source");
        var wealth = run.Match.Player.Wealth;
        var random = run.Match.Random.State;
        Require(service.MoveCard(run, owned.Id, BoardZone.Bench, 0).IsSuccess
            && run.Match.Board.Locate(owned.Id)!.Value.Zone == BoardZone.Bench
            && run.Match.Player.Wealth == wealth && run.Match.Random.State == random,
            "cross zone move preserves wealth random and identity");
        var moved = codec.Encode(run);
        run = codec.Decode(moved);
        Require(codec.Encode(run) == moved && run.Match.Board.Battlefield.Placements.Count == 0
            && run.Match.Board.Bench.Placements.Single().CardId == owned.Id, "cross zone move survives restore without duplicate placement");

        // 固定小型卡牌验证完整推挤计划，与商店抽到的尺寸无关。
        var board = new BoardService(new BoardPlacementSolver(), definitions.Sets);
        var factory = new EntityFactory();
        foreach (var card in run.Match.Player.Inventory.Cards.ToArray())
        {
            _ = board.RemoveFromBoard(run.Match, card.Id);
            run.Match.Player.Inventory.Remove(card.Id);
        }
        CardInstance Add(string key, BoardZone zone, int start)
        {
            var card = factory.CreateCard(definitions.Cards[new StringName(key)]);
            run.Match.Player.Inventory.Add(card);
            if (board.PlaceCard(run.Match, card.Id, zone, start).IsFailure) throw new InvalidOperationException("Construction fixture placement failed.");
            return card;
        }
        var first = Add("card.silver_needle_grass", BoardZone.Battlefield, 0);
        var second = Add("card.silver_needle_grass", BoardZone.Battlefield, 1);
        var third = Add("card.lava_stone", BoardZone.Bench, 0);
        Require(service.MoveCard(run, third.Id, BoardZone.Battlefield, 0).IsSuccess
            && run.Match.Board.Locate(first.Id)!.Value.Placement.Start == 1
            && run.Match.Board.Locate(second.Id)!.Value.Placement.Start == 2
            && run.Match.Board.Locate(third.Id)!.Value.Placement.Start == 0, "server move commits full push plan");
        Require(codec.Encode(codec.Decode(codec.Encode(run))) == codec.Encode(run), "pushed layout survives restore");
        var burner = Add("card.fire_slime", BoardZone.Battlefield, 5);
        third.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 3);
        third.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), third.Id, GameAttributeKeys.Value, 7));
        wealth = run.Match.Player.Wealth;
        var burnBonus = burner.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.BurnBonus);
        Require(service.SellCard(run, third.Id).IsSuccess && run.Match.Player.Wealth == wealth + 10
            && !run.Match.Board.Contains(third.Id) && run.Match.Player.Inventory.Find(third.Id) is null
            && burner.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.BurnBonus) == burnBonus + 1,
            "sale uses current value and applies reusable sale reward once");
        var sold = codec.Encode(run);
        run = codec.Decode(sold);
        Require(codec.Encode(run) == sold && run.Match.Player.Inventory.Find(burner.Id)!.Attributes.BaseCombat
            .GetFinalValue(GameAttributeKeys.BurnBonus) == burnBonus + 1, "sale reward contribution survives source removal and restore");
        Require(service.SellCard(run, third.Id).IsFailure && codec.Encode(run) == sold, "sold card cannot be sold again");
        run.Match.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, int.MaxValue);
        first = run.Match.Player.Inventory.Find(first.Id)!;
        first.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 1);
        var overflow = codec.Encode(run);
        Require(service.SellCard(run, first.Id).IsFailure && codec.Encode(run) == overflow,
            "sale overflow retains card position and attributes");

        run = codec.Decode(afterBuy);
        run.Match.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 0);
        var poor = codec.Encode(run);
        Require(service.RefreshShop(run).IsFailure && codec.Encode(run) == poor, "refresh insufficient wealth preserves stock and random");
        run.Match.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 20);
        var stock = run.Shop!;
        var keys = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).ToHashSet();
        var input = codec.Encode(run);
        var copy = codec.Decode(input);
        Require(service.CanRefreshShop(run) && codec.Encode(run) == input, "refresh availability query is read only");
        Require(service.RefreshShop(run).IsSuccess && stock.HasRefreshed && run.Match.Player.Wealth == 20 - stock.RefreshCost
            && stock.Offers.All(offer => !keys.Contains(offer.Definition.Attributes.Identity.Key))
            && stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).Distinct().Count() == stock.Offers.Count,
            "refresh charges once and excludes whole previous stock");
        Require(service.RefreshShop(copy).IsSuccess && codec.Encode(run) == codec.Encode(copy), "same saved shop refresh repeats deterministic offers");
        var refreshed = codec.Encode(run);
        run = codec.Decode(refreshed);
        Require(codec.Encode(run) == refreshed && !service.CanRefreshShop(run)
            && service.RefreshShop(run).IsFailure && codec.Encode(run) == refreshed, "restored shop cannot refresh a second time");
        Require(service.LeaveShop(run).IsSuccess, "refreshed shop can leave normally");
        before = codec.Encode(run);
        Require(service.RefreshShop(run).IsFailure && codec.Encode(run) == before, "closed shop cannot refresh");
        var cardId = run.Match.Player.Inventory.Cards.Single().Id;
        Require(service.MoveCard(run, cardId, BoardZone.Bench, 0).IsSuccess, "encounter choices allow board organization");
        run.Match.Status = MatchStatus.Lost;
        before = codec.Encode(run);
        Require(service.MoveCard(run, cardId, BoardZone.Battlefield, 0).IsFailure
            && service.SellCard(run, cardId).IsFailure && service.RefreshShop(run).IsFailure && codec.Encode(run) == before,
            "ended match rejects construction and refresh");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("construction verification: " + name);
        Console.WriteLine("SAVE_CHECK=" + name);
    }
}
