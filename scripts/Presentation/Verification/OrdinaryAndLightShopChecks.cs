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

internal static class OrdinaryAndLightShopChecks
{
    internal static bool PoolsAndRefresh()
    {
        var registry = DefinitionRegistry.Scan(typeof(OrdinaryAndLightShopChecks).Assembly);
        foreach (var definition in new CardShopEncounterDefinition[] { new OrdinaryShopEncounterDefinition(), new LightShopEncounterDefinition() })
        {
            using var image = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString()).GetImage();
            if (!image.HasMipmaps() || image.GetWidth() != image.GetHeight()) return false;
        }
        var service = new ShopCardPoolService();
        foreach (var hero in registry.Heroes.Values)
        {
            var session = Session(hero);
            var shop = new OrdinaryShopEncounterDefinition();
            if (shop.Level != 1 || shop.RequiredSize is not null || !shop.OnlyHeroFaction
                || !shop.CanAppearFor(hero.Attributes.Identity)) return false;
            var expected = registry.Cards.Values.Where(c => c.InitialLevel <= 1
                && c.Attributes.Identity.FactionKey == hero.Attributes.Identity.FactionKey).ToHashSet();
            if (!expected.SetEquals(service.GetEligibleCards(session, shop, registry.Cards.Values))) return false;
        }
        var light = new LightShopEncounterDefinition(); var paladin = new PaladinHeroDefinition();
        if (light.Level != 3 || light.OnlyHeroFaction || light.RequiredElement != GameElements.Light
            || light.RequiredSize is not null || light.CanAppearFor(null)
            || registry.Heroes.Values.Any(hero => light.CanAppearFor(hero.Attributes.Identity)
                != (hero.Attributes.Identity.Key == paladin.Attributes.Identity.Key))) return false;
        var own = Session(paladin); var pool = service.GetEligibleCards(own, light, registry.Cards.Values);
        if (!pool.ToHashSet().SetEquals(registry.Cards.Values.Where(card => card.InitialLevel <= 3
                && card.Attributes.Identity.ElementKeys.Contains(GameElements.Light)))) return false;
        CardDefinition[] sample = [new Sample("one", "paladin", [GameElements.Light]),
            new Sample("two", "harla", [GameElements.Light]), new Sample("three", "mona", [GameElements.Light, GameElements.Dark]),
            new Sample("four", "robin", [GameElements.Light]), new Sample("dark", "paladin", [GameElements.Dark]),
            new Sample("too_high", "paladin", [GameElements.Light], 4)];
        if (service.GetEligibleCards(own, light, sample).Count != 4) return false;
        var stock = service.CreateStock(own, light, sample);
        var repeat = service.CreateStock(Session(paladin), light, sample);
        if (stock.Offers.Count != 3 || stock.RefreshCost != 6 || stock.Offers.Any(o => o.Level > 3)
            || !stock.Offers.Select(o => o.Definition.Attributes.Identity.Key).SequenceEqual(repeat.Offers.Select(o => o.Definition.Attributes.Identity.Key))) return false;
        var previous = stock.Offers.Select(o => o.Definition.Attributes.Identity.Key).ToHashSet();
        if (service.Refresh(own, stock).IsFailure || own.Player.Wealth != 94 || stock.Offers.Count != 1
            || stock.Offers.Any(o => previous.Contains(o.Definition.Attributes.Identity.Key)) || service.Refresh(own, stock).IsSuccess) return false;
        var foreign = service.CreateOffer(own, light, [sample[1]]);
        return foreign is not null && new CardEconomyService(new EntityFactory()).BuyCard(own, foreign).IsSuccess
            && own.Player.Inventory.Cards.Single().Attributes.Identity.FactionKey == new StringName("harla");
    }

    internal static bool Scheduling()
    {
        var light = new LightShopEncounterDefinition(); var ordinary = new OrdinaryShopEncounterDefinition();
        var registry = DefinitionRegistry.Create([light, ordinary, new SmallShopEncounterDefinition(), new PaladinHeroDefinition(), new HarlaHeroDefinition()]);
        foreach (var hero in new HeroDefinition[] { new PaladinHeroDefinition(), new HarlaHeroDefinition() })
        foreach (var round in new[] { 1, 5 })
        {
            var session = Session(hero); session.Progress.Round = round;
            var result = new EncounterScheduler(registry).Generate(session);
            var allowed = round >= 5 && hero is PaladinHeroDefinition;
            if (result.IsFailure || result.Value!.Any(c => c.Key == light.Attributes.Identity.Key) != allowed
                || !result.Value!.Any(c => c.Key == ordinary.Attributes.Identity.Key)) return false;
        }
        return true;
    }

    private static MatchSession Session(HeroDefinition hero)
    {
        var session = new MatchSession(42, 100); session.Player.SelectHero(new EntityFactory().CreateHero(hero)); return session;
    }
    private sealed class Sample : CardDefinition
    {
        internal Sample(string key, string faction, StringName[] elements, int level = 1)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(new StringName("verify.light_shop." + key),
                "元素商店测试", new StringName(faction), CardSize.Medium, elements)), new TagSet(), initialLevel: level) { }
    }
}
