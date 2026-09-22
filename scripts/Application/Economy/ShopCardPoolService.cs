using System;
using System.Collections.Generic;
using System.Linq;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Economy;

// 生成英雄专属型号商店的可售卡牌池（应用层）。
public sealed class ShopCardPoolService
{
    // 从当前英雄可用商品池中确定性生成一件商品。
    public ShopOffer? CreateOffer(
        MatchSession session,
        ShopEncounterDefinition shop,
        IEnumerable<CardDefinition> definitions)
    {
        var cards = GetEligibleCards(session, shop, definitions);
        if (cards.Count == 0) return null;
        var random = new SeededRandom(session.Random.State);
        var card = cards[random.NextInt(0, cards.Count)];
        session.Random.State = random.State;
        return ShopOffer.Create(card);
    }

    // 按英雄归属与商店型号筛选可售卡牌。
    public IReadOnlyList<CardDefinition> GetEligibleCards(
        MatchSession session,
        ShopEncounterDefinition shop,
        IEnumerable<CardDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(definitions);
        var hero = session.Player.Hero;
        if (hero is null) return Array.Empty<CardDefinition>();

        return definitions
            .Where(card => card.Attributes.Identity.FactionKey == hero.Attributes.Identity.FactionKey
                && card.Attributes.Identity.Size == shop.CardSize)
            .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }
}
