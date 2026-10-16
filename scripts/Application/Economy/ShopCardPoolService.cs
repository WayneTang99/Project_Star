using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Economy;

// 单次商店访问的商品与刷新状态（应用层经济模块）。
public sealed class ShopStock
{
    private readonly IReadOnlyList<CardDefinition> _eligibleCards;
    private IReadOnlyList<ShopOffer> _offers;

    internal ShopStock(IReadOnlyList<CardDefinition> eligibleCards, IReadOnlyList<ShopOffer> offers, int refreshCost, int shopLevel)
    {
        _eligibleCards = eligibleCards;
        _offers = offers;
        RefreshCost = refreshCost;
        ShopLevel = shopLevel;
    }

    public IReadOnlyList<ShopOffer> Offers => _offers;
    public int RefreshCost { get; }
    public bool HasRefreshed { get; private set; }
    public bool CanRefresh => !HasRefreshed && _eligibleCards.Count > ShopCardPoolService.OfferCount;
    internal IReadOnlyList<CardDefinition> EligibleCards => _eligibleCards;
    internal int ShopLevel { get; }

    internal void ReplaceOffers(IReadOnlyList<ShopOffer> offers)
    {
        _offers = offers;
        HasRefreshed = true;
    }
}

// 生成英雄专属型号商店的可售卡牌池（应用层）。
public sealed class ShopCardPoolService
{
    public const int OfferCount = 3;
    private static readonly StringName RefreshUnavailable = new("shop.refresh_unavailable");
    private static readonly StringName InsufficientWealth = new("shop.insufficient_wealth");

    // 从当前英雄可用商品池中确定性生成一件商品。
    public ShopOffer? CreateOffer(
        MatchSession session,
        CardShopEncounterDefinition shop,
        IEnumerable<CardDefinition> definitions)
    {
        var cards = GetEligibleCards(session, shop, definitions);
        if (cards.Count == 0) return null;
        var random = new SeededRandom(session.Random.State);
        var card = cards[random.NextInt(0, cards.Count)];
        var offer = DrawOffer(card, shop.Level, random);
        session.Random.State = random.State;
        return offer;
    }

    // 创建一次最多三件、内部不重复的商店商品。
    public ShopStock CreateStock(
        MatchSession session,
        CardShopEncounterDefinition shop,
        IEnumerable<CardDefinition> definitions,
        int? shopLevelOverride = null)
    {
        var level = shopLevelOverride ?? shop.Level;
        var refreshCost = GetRefreshCost(level);
        var cards = GetEligibleCards(session, shop, definitions, level);
        return new ShopStock(cards, DrawOffers(session, cards, level), refreshCost, level);
    }

    // 支付费用并执行本次商店唯一一次刷新。
    public Result<IReadOnlyList<ShopOffer>> Refresh(MatchSession session, ShopStock stock)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(stock);
        var check = CheckRefresh(session, stock);
        if (check.IsFailure) return Result<IReadOnlyList<ShopOffer>>.Fail(check.Failure!);
        session.Player.TrySpendWealth(stock.RefreshCost);
        var offers = DrawOffers(session, GetRefreshCards(session, stock), stock.ShopLevel);
        stock.ReplaceOffers(offers);
        return Result<IReadOnlyList<ShopOffer>>.Success(offers);
    }

    // 费用与剩余次数条件由应用查询提供，提交复用同一校验。
    public Result CheckRefresh(MatchSession session, ShopStock stock)
    {
        if (!stock.CanRefresh || GetRefreshCards(session, stock).Count == 0)
            return Result.Fail(new Failure(
                RefreshUnavailable,
                "本次商店不可刷新。"));
        if (session.Player.Wealth < stock.RefreshCost)
            return Result.Fail(new Failure(
                InsufficientWealth,
                "刷新商店的金钱不足。"));
        return Result.Success();
    }

    // 按商店等级返回刷新费用。
    public static int GetRefreshCost(int shopLevel) => shopLevel switch
    {
        1 => 2,
        2 => 4,
        3 => 6,
        4 => 8,
        5 => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(shopLevel)),
    };

    // 按英雄归属、商店型号与商店等级上限筛选可售卡牌。
    public IReadOnlyList<CardDefinition> GetEligibleCards(
        MatchSession session,
        CardShopEncounterDefinition shop,
        IEnumerable<CardDefinition> definitions,
        int? shopLevelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(definitions);
        var level = shopLevelOverride ?? shop.Level;
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(shopLevelOverride));
        var hero = session.Player.Hero;
        if (hero is null) return Array.Empty<CardDefinition>();

        return ExcludeOwnedMaxLevelCards(session, definitions)
            .Where(card => card.Attributes.Identity.FactionKey == hero.Attributes.Identity.FactionKey
                && card.Attributes.Identity.Size == shop.CardSize
                && card.InitialLevel <= level)
            .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<CardDefinition> GetRefreshCards(MatchSession session, ShopStock stock)
    {
        var previous = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).ToHashSet();
        return ExcludeOwnedMaxLevelCards(session, stock.EligibleCards)
            .Where(card => !previous.Contains(card.Attributes.Identity.Key)).ToList().AsReadOnly();
    }

    private static IReadOnlyList<ShopOffer> DrawOffers(
        MatchSession session,
        IReadOnlyList<CardDefinition> definitions,
        int shopLevel)
    {
        var remaining = definitions.ToList();
        var random = new SeededRandom(session.Random.State);
        var offers = new List<ShopOffer>(Math.Min(OfferCount, remaining.Count));
        while (offers.Count < OfferCount && remaining.Count > 0)
        {
            var index = random.NextInt(0, remaining.Count);
            offers.Add(DrawOffer(remaining[index], shopLevel, random));
            remaining.RemoveAt(index);
        }
        session.Random.State = random.State;
        return offers.AsReadOnly();
    }

    private static ShopOffer DrawOffer(CardDefinition card, int shopLevel, SeededRandom random)
    {
        var levels = Enumerable.Range(card.InitialLevel, shopLevel - card.InitialLevel + 1)
            .Where(card.SupportsLevel).ToArray();
        var level = levels.Length == 1 ? levels[0] : levels[random.NextInt(0, levels.Length)];
        return ShopOffer.Create(card, level);
    }

    private static IReadOnlyList<CardDefinition> ExcludeOwnedMaxLevelCards(
        MatchSession session, IEnumerable<CardDefinition> definitions)
    {
        var owned = session.Player.Inventory.Cards.Where(card => card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) >= 4)
            .Select(card => card.Attributes.Identity.Key).ToHashSet();
        return definitions.Where(card => !owned.Contains(card.Attributes.Identity.Key)).ToList().AsReadOnly();
    }
}
