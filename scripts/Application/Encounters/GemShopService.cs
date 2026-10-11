using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Encounters;

public sealed record SocketShopGemCommand(Guid MatchId, EntityId CardId, int OfferIndex, long Revision);

// 渠道负责报价、刷新及一次镶嵌授权；预览和提交共用校验，失败不扣钱或消耗随机数。
public sealed class GemShopService
{
    private readonly IDefinitionCatalog _catalog;
    private readonly SocketGemService _sockets = new();
    public GemShopService(IDefinitionCatalog catalog) => _catalog = catalog;

    public Result Open(MatchSession session, GemShopDefinition definition)
    {
        if (session.Status != MatchStatus.InProgress) return Fail("ended", "对局已结束。");
        session.ActiveGemShopVisit = new GemShopVisit(definition, Draw(session, definition.OfferCount), ++session.GemShopRevision);
        return Result.Success();
    }
    public void Close(MatchSession session)
    {
        if (session.ActiveGemShopVisit is { } visit) session.GemShopRevision = Math.Max(session.GemShopRevision, visit.Revision);
        session.ActiveGemShopVisit = null;
    }
    public Result CheckRefresh(MatchSession session)
    {
        var visit = session.ActiveGemShopVisit;
        if (session.Status != MatchStatus.InProgress || visit is null || visit.IsResolved || visit.HasRefreshed)
            return Fail("refresh_unavailable", "本次商店不可刷新。");
        if (session.Player.Wealth < ShopCardPoolService.GetRefreshCost(visit.Definition.Level))
            return Fail("wealth", "刷新商店的金币不足。");
        return Result.Success();
    }
    public Result Refresh(MatchSession session)
    {
        var check = CheckRefresh(session); if (check.IsFailure) return check;
        var visit = session.ActiveGemShopVisit!;
        var offers = Draw(session, visit.Definition.OfferCount);
        session.Player.TrySpendWealth(ShopCardPoolService.GetRefreshCost(visit.Definition.Level));
        visit.Refresh(offers);
        return Result.Success();
    }
    public Result CheckSocket(MatchSession session, SocketShopGemCommand command)
    {
        var visit = session.ActiveGemShopVisit;
        if (command.MatchId != session.Id || session.Status != MatchStatus.InProgress || visit is null
            || command.Revision != visit.Revision || visit.IsResolved)
            return Fail("stale", "本次镶嵌已失效或已经完成。");
        if (command.OfferIndex < 0 || command.OfferIndex >= visit.Offers.Count) return Fail("offer", "砧板不存在。");
        var card = session.Player.Inventory.Cards.FirstOrDefault(card => card.Id == command.CardId);
        if (card is null || !session.Board.Contains(command.CardId))
            return Fail("card", "请拖动己方棋盘卡牌进行镶嵌。");
        var socket = FirstEmptySocket(card);
        if (socket < 0) return Fail("full", "此卡牌没有空宝石孔。");
        var check = _sockets.Check(session, new SocketGemCommand(session.Id, card.Id, socket, visit.Offers[command.OfferIndex]));
        if (check.IsFailure) return check;
        return session.Player.Wealth < visit.Definition.SocketCost ? Fail("wealth", $"镶嵌需要{visit.Definition.SocketCost}金币，当前金币不足。") : Result.Success();
    }
    public Result Socket(MatchSession session, SocketShopGemCommand command)
    {
        var check = CheckSocket(session, command); if (check.IsFailure) return check;
        var visit = session.ActiveGemShopVisit!;
        var card = session.Player.Inventory.Cards.Single(card => card.Id == command.CardId);
        var result = _sockets.Execute(session, new SocketGemCommand(session.Id, card.Id, FirstEmptySocket(card), visit.Offers[command.OfferIndex]));
        if (result.IsFailure) return result;
        session.Player.TrySpendWealth(visit.Definition.SocketCost);
        visit.Resolve();
        return Result.Success();
    }
    private IReadOnlyList<GemDefinition> Draw(MatchSession session, int count)
    {
        var pool = _catalog.Gems.Values.OrderBy(gem => gem.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToList();
        var random = new SeededRandom(session.Random.State); var offers = new List<GemDefinition>();
        while (pool.Count > 0 && offers.Count < count)
        { var index = random.NextInt(0, pool.Count); offers.Add(pool[index]); pool.RemoveAt(index); }
        session.Random.State = random.State;
        return offers.AsReadOnly();
    }
    private static int FirstEmptySocket(CardInstance card)
    { for (var index = 0; index < card.GemSockets.Count; index++) if (card.GemSockets[index] is null) return index; return -1; }
    private static Result Fail(string key, string message) => Result.Fail(new Failure(new StringName("gem_shop." + key), message));
}
