using System;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

internal static class EquipmentShopEncounterChecks
{
    // 不同遭遇类型共用同一配置，按英雄key而非职业文本／归属判断。
    public static bool GenericHeroEligibility()
    {
        var paladin = new PaladinHeroDefinition(); var harla = new HarlaHeroDefinition();
        var paladinKey = paladin.Attributes.Identity.Key; var harlaKey = harla.Attributes.Identity.Key;
        var source = new[] { paladinKey };
        var restricted = new Restricted("restricted", EncounterKind.Other, source);
        source[0] = harlaKey;
        if (!restricted.CanAppearFor(paladin.Attributes.Identity) || restricted.CanAppearFor(harla.Attributes.Identity)
            || restricted.CanAppearFor(null) || restricted.CanAppearFor(new HeroIdentityAttributes(new StringName("hero.other"),
                "同职业其他英雄", paladin.Attributes.Identity.FactionKey))) return false;
        var unrestricted = new Restricted("open", EncounterKind.Other);
        if (!unrestricted.CanAppearFor(null) || !unrestricted.CanAppearFor(harla.Attributes.Identity)) return false;
        try { ((System.Collections.Generic.IList<StringName>)restricted.Attributes.Identity.AllowedHeroKeys)[0] = harlaKey; return false; }
        catch (NotSupportedException) { }
        try { _ = new Restricted("empty", EncounterKind.Other, [new StringName("")]); return false; }
        catch (ArgumentException) { }
        try { _ = new Restricted("duplicate", EncounterKind.Other, [paladinKey, paladinKey]); return false; }
        catch (ArgumentException) { }
        try { _ = DefinitionRegistry.Create([restricted]); return false; }
        catch (DefinitionValidationException) { }
        var registry = DefinitionRegistry.Create([restricted, unrestricted, new SmallShopEncounterDefinition(), paladin, harla]);
        foreach (var hero in new HeroDefinition[] { paladin, harla })
        {
            var session = new MatchSession(7); session.Player.SelectHero(new EntityFactory().CreateHero(hero));
            var choices = new EncounterScheduler(registry).Generate(session);
            if (choices.IsFailure || choices.Value!.Any(choice => choice.Key == restricted.Attributes.Identity.Key)
                != (hero == paladin)) return false;
        }
        var blocked = new Restricted("a_blocked", EncounterKind.Pvp, [harlaKey]);
        var allowed = new Restricted("z_allowed", EncounterKind.Pvp, [paladinKey]);
        registry = DefinitionRegistry.Create([blocked, allowed, paladin, harla]);
        var pvp = new MatchSession(7); pvp.Player.SelectHero(new EntityFactory().CreateHero(paladin)); pvp.Progress.Turn = 8;
        var pvpResult = new EncounterScheduler(registry).Generate(pvp);
        if (pvpResult.IsFailure || pvpResult.Value!.Single().Key != allowed.Attributes.Identity.Key) return false;
        var monsters = new[] { new Restricted("m1", EncounterKind.Monster, [paladinKey]),
            new Restricted("m2", EncounterKind.Monster), new Restricted("m3", EncounterKind.Monster),
            new Restricted("m_blocked", EncounterKind.Monster, [harlaKey]) };
        registry = DefinitionRegistry.Create(monsters.Cast<object>().Concat(new object[] { paladin, harla }));
        var monsterSession = new MatchSession(7); monsterSession.Player.SelectHero(new EntityFactory().CreateHero(paladin)); monsterSession.Progress.Turn = 4;
        var result = new EncounterScheduler(registry).Generate(monsterSession);
        return result.IsSuccess && result.Value!.Count == 3 && result.Value.All(choice => choice.Key != monsters[3].Attributes.Identity.Key);
    }

    private sealed class Restricted : EncounterDefinition
    {
        public Restricted(string key, EncounterKind kind, StringName[]? heroes = null)
            : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
                new StringName("verify.encounter." + key), "资格测试遭遇", allowedHeroKeys: heroes)), 1, 99, kind) { }
    }

    public static bool EligibilityAndScheduling()
    {
        var registry = DefinitionRegistry.Scan(typeof(EquipmentShopEncounterChecks).Assembly);
        var shop = new EquipmentShopEncounterDefinition();
        if (!registry.Encounters.ContainsKey(shop.Attributes.Identity.Key) || shop.Level != 1
            || shop.RequiredSize is not null || shop.OnlyHeroFaction || shop.RequiredTag != GameTags.Equipment
            || !ResourceLoader.Exists(shop.Attributes.Identity.Illustration.ToString()) || shop.CanAppearFor(null)) return false;
        var pool = DefinitionRegistry.Create([shop, new SmallShopEncounterDefinition(), new MediumShopEncounterDefinition(), new PaladinHeroDefinition(), new HarlaHeroDefinition()]);
        foreach (var hero in registry.Heroes.Values)
        {
            var expected = hero.Attributes.Identity.FactionKey == new StringName("paladin")
                || hero.Attributes.Identity.FactionKey == new StringName("harla");
            if (shop.CanAppearFor(hero.Attributes.Identity) != expected) return false;
            for (var seed = 1; seed <= 12; seed++)
            {
                var session = new MatchSession((ulong)seed, 100);
                session.Player.SelectHero(new EntityFactory().CreateHero(hero));
                var result = new EncounterScheduler(pool).Generate(session);
                if (result.IsFailure || result.Value!.Any(choice => choice.Key == shop.Attributes.Identity.Key) != expected) return false;
            }
        }
        return true;
    }

    public static bool PoolRefreshAndPurchase()
    {
        var shop = new EquipmentShopEncounterDefinition(); var service = new ShopCardPoolService();
        CardDefinition[] cards = [new Sample("one", "paladin", CardSize.Small), new Sample("two", "harla", CardSize.Medium),
            new Sample("three", "mona", CardSize.Large), new Sample("neutral", "neutral", CardSize.Small),
            new Sample("not_equipment", "harla", CardSize.Small, equipment: false), new Sample("level_two", "mona", CardSize.Large, level: 2)];
        MatchSession Session()
        {
            var session = new MatchSession(42, 100); session.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition())); return session;
        }
        var session = Session(); var eligible = service.GetEligibleCards(session, shop, cards);
        if (eligible.Count != 4 || eligible.Any(card => !card.Tags.Contains(GameTags.Equipment) || card.InitialLevel > 1)
            || eligible.Select(card => card.Attributes.Identity.Size).Distinct().Count() != 3
            || eligible.Select(card => card.Attributes.Identity.FactionKey).Distinct().Count() != 4) return false;
        var stock = service.CreateStock(session, shop, cards); var replay = service.CreateStock(Session(), shop, cards);
        if (stock.Offers.Count != 3 || stock.Offers.Any(offer => offer.Level != 1)
            || !stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).SequenceEqual(replay.Offers.Select(offer => offer.Definition.Attributes.Identity.Key))) return false;
        var previous = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).ToHashSet();
        if (service.Refresh(session, stock).IsFailure || stock.Offers.Count != 1 || session.Player.Wealth != 98
            || stock.Offers.Any(offer => previous.Contains(offer.Definition.Attributes.Identity.Key) || offer.Level != 1)
            || service.Refresh(session, stock).IsSuccess) return false;
        var foreign = service.CreateOffer(session, shop, [cards[2]]);
        if (foreign is null || new CardEconomyService(new EntityFactory()).BuyCard(session, foreign).IsFailure
            || !session.Player.Inventory.Cards.Any(card => card.Attributes.Identity.Key == cards[2].Attributes.Identity.Key)) return false;
        session.Player.Inventory.Add(new EntityFactory().CreateCard(cards[0], 4));
        return !service.GetEligibleCards(session, shop, cards).Contains(cards[0])
            && service.CreateStock(session, shop, [cards[4], cards[5]]).Offers.Count == 0;
    }

    private sealed class Sample : CardDefinition
    {
        public Sample(string key, string faction, CardSize size, bool equipment = true, int level = 1)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(new StringName("verify.equipment_shop." + key),
                "装备商店测试", new StringName(faction), size, [GameElements.General])),
                TagSet.ForCard(size, equipment ? [GameTags.Equipment] : [GameTags.Human]), initialLevel: level) { }
    }
}

