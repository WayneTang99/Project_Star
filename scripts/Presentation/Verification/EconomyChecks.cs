using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 收入、合并、获得与交易原子性验证（表现层验证模块）。
internal static class EconomyChecks
{
    internal static bool CheckIndependentInstances()
    {
        var factory = new EntityFactory();
        var definition = new VerificationCardDefinition();
        var first = factory.CreateCard(definition);
        var second = factory.CreateCard(definition);
        var value = new StringName("Value");
        first.Attributes.Persistent.SetBaseValue(value, 25);

        return first.Id != second.Id
            && first.Attributes.Persistent.GetBaseValue(value) == 25
            && second.Attributes.Persistent.GetBaseValue(value) == 0;
    }

    internal static bool CheckMatchCreation()
    {
        var factory = new EntityFactory();
        var session = new MatchSession(42);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        session.Player.Inventory.Add(factory.CreateCard(new VerificationCardDefinition()));

        return session.Player.Hero is not null
            && session.Player.Inventory.Cards.Count == 1
            && session.Board.Battlefield.Count == 0
            && session.Board.Bench.Count == 0
            && session.Random.Seed == 42;
    }

    internal static bool CheckSessionIsolation()
    {
        var factory = new EntityFactory();
        var first = new MatchSession(1);
        var second = new MatchSession(1);
        first.Player.Inventory.Add(factory.CreateCard(new VerificationCardDefinition()));

        return first.Id != second.Id
            && first.Player.Inventory.Cards.Count == 1
            && second.Player.Inventory.Cards.Count == 0
            && !ReferenceEquals(first.Board, second.Board)
            && !ReferenceEquals(first.EncounterSchedule, second.EncounterSchedule);
    }

    internal static bool CheckRoundIncome()
    {
        var session = new CreateMatchService(new EntityFactory()).Create(
            1,
            10,
            new VerificationHeroDefinition());
        var service = new RoundIncomeService();
        var repeated = service.SettleCurrentRound(session);
        session.Progress.Round = 2;
        var nextRound = service.SettleCurrentRound(session);
        var snapshot = MatchSnapshot.From(session);
        return session.Player.Income == 5
            && repeated.Value == 0
            && nextRound.Value == 5
            && session.Player.Wealth == 20
            && session.Progress.IncomeSettledThroughRound == 2
            && snapshot.Income == 5
            && session.Events.Count(value => value is IncomeGrantedEvent) == 2;
    }

    internal static bool CheckInitialValues()
    {
        var small = new EconomyCardDefinition(CardSize.Small);
        var medium = new EconomyCardDefinition(CardSize.Medium);
        var large = new EconomyCardDefinition(CardSize.Large);
        var levelFiveSmall = new FiveLevelEconomyCardDefinition(CardSize.Small);
        var levelFiveMedium = new FiveLevelEconomyCardDefinition(CardSize.Medium);
        var levelFiveLarge = new FiveLevelEconomyCardDefinition(CardSize.Large);
        return CardValueCalculator.CalculateInitialValue(small, 1) == 2
            && CardValueCalculator.CalculateInitialValue(small, 2) == 4
            && CardValueCalculator.CalculateInitialValue(small, 3) == 8
            && CardValueCalculator.CalculateInitialValue(small, 4) == 16
            && CardValueCalculator.CalculateInitialValue(levelFiveSmall, 5) == 16
            && CardValueCalculator.CalculateInitialValue(medium, 1) == 4
            && CardValueCalculator.CalculateInitialValue(medium, 2) == 8
            && CardValueCalculator.CalculateInitialValue(medium, 3) == 16
            && CardValueCalculator.CalculateInitialValue(medium, 4) == 32
            && CardValueCalculator.CalculateInitialValue(levelFiveMedium, 5) == 32
            && CardValueCalculator.CalculateInitialValue(large, 1) == 6
            && CardValueCalculator.CalculateInitialValue(large, 2) == 12
            && CardValueCalculator.CalculateInitialValue(large, 3) == 24
            && CardValueCalculator.CalculateInitialValue(large, 4) == 48
            && CardValueCalculator.CalculateInitialValue(levelFiveLarge, 5) == 48;
    }

    internal static bool CheckSizeShopCardPools()
    {
        var session = new MatchSession(1);
        session.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        CardDefinition[] cards =
        [
            new BeastHideCardDefinition(),
            new LightCavalryCardDefinition(),
            new ArcaneShieldCardDefinition(),
            new MilitaryBootsCardDefinition(),
            new JudgmentHammerCardDefinition(),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin")),
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("other")),
        ];
        var service = new ShopCardPoolService();
        var smallShop = new SmallShopEncounterDefinition();
        var mediumShop = new MediumShopEncounterDefinition();
        var largeShop = new LargeShopEncounterDefinition();
        var small = service.GetEligibleCards(session, smallShop, cards);
        var medium = service.GetEligibleCards(session, mediumShop, cards);
        var large = service.GetEligibleCards(session, largeShop, cards);

        var refreshSession = new MatchSession(2, 10);
        refreshSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        CardDefinition[] refreshCards =
        [
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "one"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "two"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "three"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "four"),
        ];
        var stock = service.CreateStock(refreshSession, smallShop, refreshCards);
        var initialUnique = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).Distinct().Count() == 3;
        var previousKeys = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).ToHashSet();
        var refreshed = service.Refresh(refreshSession, stock);
        var refreshedUnique = stock.Offers.All(offer => !previousKeys.Contains(offer.Definition.Attributes.Identity.Key));
        var repeatedRefresh = service.Refresh(refreshSession, stock);

        var limitedSession = new MatchSession(3, 10);
        limitedSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        var limitedStock = service.CreateStock(limitedSession, mediumShop,
        [
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("paladin"), "one"),
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("paladin"), "two"),
        ]);
        var limitedRefresh = service.Refresh(limitedSession, limitedStock);

        var poorSession = new MatchSession(4, 1);
        poorSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        var poorStock = service.CreateStock(poorSession, smallShop, refreshCards);
        var poorKeys = string.Join("|", poorStock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key));
        var poorRefresh = service.Refresh(poorSession, poorStock);

        return smallShop.Kind == EncounterKind.Shop
            && smallShop.RequiredSize == CardSize.Small
            && smallShop.Level == 1
            && mediumShop.RequiredSize == CardSize.Medium
            && mediumShop.Level == 1
            && largeShop.RequiredSize == CardSize.Large
            && largeShop.Level == 2
            && small.Count == 2
            && small.All(card => card.Attributes.Identity.FactionKey == new StringName("paladin")
                && card.Attributes.Identity.Size == CardSize.Small)
            && small.Any(card => card.Attributes.Identity.Key == new StringName("card.military_boots"))
            && medium.Count == 1
            && medium.Any(card => card.Attributes.Identity.Key == new StringName("card.light_cavalry"))
            && !medium.Any(card => card.Attributes.Identity.Key == new StringName("card.arcane_shield"))
            && large.Count == 0
            && stock.Offers.Count == 1
            && initialUnique
            && refreshed.IsSuccess
            && refreshedUnique
            && stock.HasRefreshed
            && !stock.CanRefresh
            && refreshSession.Player.Wealth == 8
            && repeatedRefresh.IsFailure
            && limitedStock.Offers.Count == 2
            && !limitedStock.CanRefresh
            && limitedRefresh.IsFailure
            && limitedSession.Player.Wealth == 10
            && poorRefresh.IsFailure
            && poorSession.Player.Wealth == 1
            && !poorStock.HasRefreshed
            && poorKeys == string.Join("|", poorStock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key))
            && ShopCardPoolService.GetRefreshCost(1) == 2
            && ShopCardPoolService.GetRefreshCost(2) == 4
            && ShopCardPoolService.GetRefreshCost(3) == 6
            && ShopCardPoolService.GetRefreshCost(4) == 8
            && ShopCardPoolService.GetRefreshCost(5) == 10;
    }

    internal static bool CheckShopLevelLimit()
    {
        var faction = new StringName("paladin");
        CardDefinition[] cards = Enumerable.Range(1, 5)
            .Select(level => (CardDefinition)new ShopVerificationCardDefinition(CardSize.Small, faction, $"level{level}", level))
            .Concat(Enumerable.Range(1, 3).Select(index =>
                new ShopVerificationCardDefinition(CardSize.Small, faction, $"extra{index}"))).ToArray();
        var service = new ShopCardPoolService();
        for (var shopLevel = 1; shopLevel <= 4; shopLevel++)
        {
            var session = new MatchSession(42, 100);
            session.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
            var shop = new LevelShopVerificationDefinition(shopLevel);
            var stock = service.CreateStock(session, shop, cards);
            if (service.GetEligibleCards(session, shop, cards).Count != shopLevel + 3
                || stock.Offers.Count != 3 || !stock.CanRefresh
                || stock.Offers.Any(offer => offer.Level > shopLevel || offer.Level < offer.Definition.InitialLevel
                    || !offer.Definition.SupportsLevel(offer.Level))
                || service.CreateOffer(session, shop, cards) is not { } offer || offer.Level > shopLevel) return false;
            var previous = stock.Offers.Select(item => item.Definition.Attributes.Identity.Key).ToHashSet();
            var refresh = service.Refresh(session, stock);
            if (stock.Offers.Any(item => previous.Contains(item.Definition.Attributes.Identity.Key)) || refresh.IsFailure || stock.Offers.Any(item => item.Level > shopLevel)
                || session.Player.Wealth != 100 - ShopCardPoolService.GetRefreshCost(shopLevel)) return false;
            // 等级恰好等于上限可以出现，高于上限的卡池为空且不推进随机状态。
            var boundary = cards[shopLevel - 1];
            if (service.CreateOffer(session, shop, [boundary])?.Level != shopLevel) return false;
            var randomBefore = session.Random.State;
            if (service.CreateOffer(session, shop, [cards[shopLevel]]) is not null
                || service.CreateStock(session, shop, [cards[shopLevel]]).Offers.Count != 0
                || session.Random.State != randomBefore) return false;
            // 刷新资格也只计算过滤后的商品池。
            var limited = service.CreateStock(session, shop, [cards[0], cards[4]]);
            if (limited.Offers.Count != 1 || limited.CanRefresh || limited.Offers[0].Level > shopLevel) return false;
        }
        var testSession = new MatchSession(42, 100);
        testSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        var ordinaryShop = new LevelShopVerificationDefinition(1);
        var testStock = service.CreateStock(testSession, ordinaryShop, cards, shopLevelOverride: 5);
        if (ordinaryShop.Level != 1 || testStock.RefreshCost != 10
            || service.GetEligibleCards(testSession, ordinaryShop, cards, 5).Count != 8
            || service.GetEligibleCards(testSession, ordinaryShop, cards).Count != 4) return false;
        return service.Refresh(testSession, testStock).IsSuccess && testSession.Player.Wealth == 90
            && testStock.Offers.All(offer => offer.Level <= 5 && offer.Level >= offer.Definition.InitialLevel
                && offer.Definition.SupportsLevel(offer.Level))
            && CheckShopHigherLevelsAndOwnership();
    }

    private static bool CheckShopHigherLevelsAndOwnership()
    {
        var factory = new EntityFactory();
        var service = new ShopCardPoolService();
        var shop = new LevelShopVerificationDefinition(4);
        var cards = Enumerable.Range(0, 6).Select(index => (CardDefinition)new ShopVerificationCardDefinition(
            CardSize.Small, new StringName("paladin"), $"owned{index}")).ToArray();
        var session = new MatchSession(42, 100);
        var replay = new MatchSession(42, 100);
        session.Player.SelectHero(factory.CreateHero(new PaladinHeroDefinition()));
        replay.Player.SelectHero(factory.CreateHero(new PaladinHeroDefinition()));
        var seenLevels = new System.Collections.Generic.HashSet<int>();
        for (var draw = 0; draw < 64; draw++)
        {
            var offer = service.CreateOffer(session, shop, [cards[0]])!;
            var repeated = service.CreateOffer(replay, shop, [cards[0]])!;
            if (offer.Level != repeated.Level || session.Random.State != replay.Random.State
                || offer.Price != CardValueCalculator.CalculateInitialValue(cards[0], offer.Level)) return false;
            seenLevels.Add(offer.Level);
        }
        if (!seenLevels.SetEquals([1, 2, 3, 4])) return false;
        session.Player.Inventory.Add(factory.CreateCard(cards[0], 4));
        session.Player.Inventory.Add(factory.CreateCard(cards[1], 3));
        if (service.GetEligibleCards(session, shop, cards).Count != 5
            || service.CreateOffer(session, shop, [cards[0]]) is not null) return false;
        // 进入商店后再获得满级卡牌，刷新也必须重新检查当前库存。
        var stock = service.CreateStock(session, shop, cards);
        session.Player.Inventory.Add(factory.CreateCard(cards[2], 4));
        var refresh = service.Refresh(session, stock);
        if (refresh.IsFailure || stock.Offers.Any(offer => offer.Definition == cards[0] || offer.Definition == cards[2])) return false;
        var limited = service.CreateStock(session, shop, cards);
        foreach (var card in cards.Where(card => !limited.Offers.Any(offer => offer.Definition == card)))
            session.Player.Inventory.Add(factory.CreateCard(card, 4));
        var wealth = session.Player.Wealth;
        var random = session.Random.State;
        if (service.Refresh(session, limited).IsSuccess || session.Player.Wealth != wealth || session.Random.State != random) return false;
        foreach (var card in cards.Skip(1)) session.Player.Inventory.Add(factory.CreateCard(card, 4));
        return service.CreateStock(session, shop, cards).Offers.Count == 0
            && session.Random.State == random;
    }

    // 可指定等级的验证商店，不进入正式遭遇池（表现层验证模块）。
    private sealed class LevelShopVerificationDefinition : CardShopEncounterDefinition
    {
        public LevelShopVerificationDefinition(int level)
            : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
                new StringName("verification.shop.level_limit"), "等级上限验证商店")), CardSize.Small, level) { }
    }

    internal static bool CheckSpecialValueCoefficient()
    {
        return CardValueCalculator.CalculateInitialValue(new EconomyCardDefinition(CardSize.Medium, 4), 3) == 32;
    }

    internal static bool CheckAcquisitionSources()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var definition = new EconomyCardDefinition(CardSize.Small);
        var session = new MatchSession(1);

        var purchased = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Purchase);
        var dropped = economy.AcquireCard(new MatchSession(2), definition, 1, CardAcquisitionSource.Drop);
        var rewarded = economy.AcquireCard(new MatchSession(3), definition, 1, CardAcquisitionSource.Reward);

        return purchased.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1
            && dropped.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1
            && rewarded.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1;
    }

    internal static bool CheckAcquiredValueRounding() => CardValueCalculator.CalculateAcquiredValue(3) == 1;

    internal static bool CheckLevelDoesNotRecalculateValue()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1);
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Reward).Value!;

        card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 4);
        return card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1;
    }

    internal static bool CheckCardMergeUpgrade()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var economy = new CardEconomyService(factory, board);
        var definition = new LightCavalryCardDefinition();
        var session = new MatchSession(1);
        var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(session, first.Card.Id, BoardZone.Battlefield, 0);
        var existingLevelTwo = factory.CreateCard(definition, 2);
        session.Player.Inventory.Add(existingLevelTwo);
        _ = board.PlaceCard(session, existingLevelTwo.Id, BoardZone.Battlefield, 2);
        var merged = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
        var location = session.Board.Locate(existingLevelTwo.Id);
        var firstLevelFour = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!;
        var secondLevelFour = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!;

        return merged.WasUpgraded
            && merged.Card.Id == existingLevelTwo.Id
            && merged.PreviousLevel == 2
            && merged.CurrentLevel == 3
            && merged.Card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && merged.Card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 40
            && merged.Card.Abilities[0].CooldownTicks == 30
            && location?.Zone == BoardZone.Battlefield
            && location?.Placement.Start == 2
            && session.Player.Inventory.Find(first.Card.Id) is null
            && firstLevelFour.WasCreated
            && secondLevelFour.WasCreated
            && firstLevelFour.Card.Id != secondLevelFour.Card.Id;
    }

    internal static bool CheckCardLevelDefinitions()
    {
        var definition = new JudgmentHammerCardDefinition();
        var factory = new EntityFactory();
        var levelThree = factory.CreateCard(definition);
        var levelFour = factory.CreateCard(definition, 4);
        return definition.InitialLevel == 3
            && !definition.SupportsLevel(1)
            && !definition.SupportsLevel(2)
            && definition.SupportsLevel(3)
            && definition.SupportsLevel(4)
            && !definition.SupportsLevel(5)
            && levelThree.Attributes.Identity.Size == CardSize.Large
            && levelThree.Attributes.Identity.ElementKeys[0] == GameElements.Light
            && levelThree.Tags.Contains(GameTags.Equipment)
            && levelThree.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && levelThree.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.CooldownTicks) == 60
            && levelThree.Abilities[0].CooldownTicks == 60
            && levelFour.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
            && levelFour.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.CooldownTicks) == 50
            && levelFour.Abilities[0].CooldownTicks == 50;
    }

    internal static bool CheckBeastHideDefinition()
    {
        var definition = new BeastHideCardDefinition();
        var factory = new EntityFactory();
        var card = factory.CreateCard(definition);
        var registry = DefinitionRegistry.Create([new VerificationHeroDefinition(), definition]);
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        player.Player.Inventory.Add(card);
        _ = CreateBoardService().PlaceCard(player, card.Id, BoardZone.Battlefield, 0);
        var battleSetup = new BattleSetupFactory().Create(player, opponent, 1, new BattleTick(10));
        var economy = new CardEconomyService(factory);
        var levelOne = economy.AcquireCard(new MatchSession(3), definition, 1, CardAcquisitionSource.Reward).Value!;
        var levelTwo = economy.AcquireCard(new MatchSession(4), definition, 2, CardAcquisitionSource.Reward).Value!;
        var levelThree = economy.AcquireCard(new MatchSession(5), definition, 3, CardAcquisitionSource.Reward).Value!;
        var levelFour = economy.AcquireCard(new MatchSession(6), definition, 4, CardAcquisitionSource.Reward).Value!;
        return registry.Cards.ContainsKey(new StringName("card.beast_hide"))
            && definition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && definition.Attributes.Identity.ElementKeys.Count == 1
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Attributes.Identity.Size == CardSize.Small
            && card.Tags.Contains(GameTags.Small)
            && card.Tags.Contains(GameTags.Material)
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && card.Abilities.Count == 0
            && battleSetup.Player.Cards.Count == 1
            && !battleSetup.Player.Cards[0].UseLegacyAttack
            && CardValueCalculator.CalculateInitialValue(definition, 1) == 2
            && CardValueCalculator.CalculateInitialValue(definition, 2) == 4
            && CardValueCalculator.CalculateInitialValue(definition, 3) == 8
            && CardValueCalculator.CalculateInitialValue(definition, 4) == 16
            && levelOne.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 3
            && levelTwo.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 6
            && levelThree.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 12
            && levelFour.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 24;
    }

    internal static bool CheckDiamondDefinition()
    {
        var definition = new DiamondCardDefinition();
        var session = new MatchSession(7);
        var card = new CardEconomyService(new EntityFactory()).AcquireCard(
            session,
            definition,
            definition.InitialLevel,
            CardAcquisitionSource.Reward).Value!;
        return definition.InitialLevel == 4
            && !definition.SupportsLevel(1)
            && !definition.SupportsLevel(2)
            && !definition.SupportsLevel(3)
            && definition.SupportsLevel(4)
            && !definition.SupportsLevel(5)
            && definition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && definition.Attributes.Identity.Size == CardSize.Small
            && definition.Attributes.Identity.ElementKeys.Count == 1
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Tags.Contains(GameTags.Small)
            && definition.Tags.Contains(GameTags.Material)
            && card.Abilities.Count == 0
            && CardValueCalculator.CalculateInitialValue(definition, 4) == 16
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 28;
    }

    internal static bool CheckJewelryBagSaleReward()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var bagDefinition = new JewelryBagCardDefinition();
        var beastHideDefinition = new BeastHideCardDefinition();
        var definitions = new CardDefinition[]
        {
            bagDefinition,
            beastHideDefinition,
            new DiamondCardDefinition(),
        };
        var economy = new CardEconomyService(factory, board, definitions);

        var session = new MatchSession(11);
        var bag = economy.AcquireCard(session, bagDefinition, 2, CardAcquisitionSource.Reward).Value!;
        var sale = economy.SellCard(session, bag.Id);
        var rewarded = session.Player.Inventory.Cards.Count == 1
            ? session.Player.Inventory.Cards[0]
            : null;

        var fullSession = new MatchSession(12, boardCapacity: 1);
        var blocker = economy.AcquireCard(fullSession, beastHideDefinition, 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(fullSession, blocker.Id, BoardZone.Battlefield, 0);
        var benchBlocker = economy.AcquireCard(fullSession, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(fullSession, benchBlocker.Id, BoardZone.Bench, 0);
        var fullBag = economy.AcquireCard(fullSession, bagDefinition, 2, CardAcquisitionSource.Reward).Value!;
        var fullSale = economy.SellCard(fullSession, fullBag.Id);
        var canRewardDiamondAtLevelFour = false;
        for (ulong seed = 1; seed <= 32 && !canRewardDiamondAtLevelFour; seed++)
        {
            var levelFourSession = new MatchSession(seed);
            var levelFourBag = economy.AcquireCard(
                levelFourSession,
                bagDefinition,
                4,
                CardAcquisitionSource.Reward).Value!;
            _ = economy.SellCard(levelFourSession, levelFourBag.Id);
            canRewardDiamondAtLevelFour = levelFourSession.Player.Inventory.Cards.Count == 1
                && levelFourSession.Player.Inventory.Cards[0].Attributes.Identity.Key
                    == new StringName("card.diamond");
        }

        return bagDefinition.InitialLevel == 2
            && !bagDefinition.SupportsLevel(1)
            && bagDefinition.SupportsLevel(2)
            && bagDefinition.SupportsLevel(3)
            && bagDefinition.SupportsLevel(4)
            && !bagDefinition.SupportsLevel(5)
            && bagDefinition.Tags.Count == 1
            && bagDefinition.Tags.Contains(GameTags.Small)
            && bagDefinition.OnSellReward is RandomTaggedCardOnSellDefinition
            && sale.IsSuccess
            && session.Player.Wealth == 2
            && rewarded is not null
            && rewarded.Attributes.Identity.Key == new StringName("card.beast_hide")
            && rewarded.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2
            && session.Board.Battlefield.Count == 1
            && session.Board.Bench.Count == 0
            && fullSale.IsSuccess
            && fullSession.Player.Wealth == 2
            && fullSession.Player.Inventory.Cards.Count == 2
            && fullSession.Player.Inventory.Find(blocker.Id) is not null
            && fullSession.Player.Inventory.Find(benchBlocker.Id) is not null
            && canRewardDiamondAtLevelFour;
    }

    internal static bool CheckTreasureChestSaleReward()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var chestDefinition = new TreasureChestCardDefinition();
        CardDefinition[] definitions =
        [
            chestDefinition,
            new BeastHideCardDefinition(),
            new DiamondCardDefinition(),
        ];
        var economy = new CardEconomyService(factory, board, definitions);
        var session = new MatchSession(21);
        var chest = economy.AcquireCard(session, chestDefinition, 4, CardAcquisitionSource.Reward).Value!;
        var sale = economy.SellCard(session, chest.Id);
        var reward = chestDefinition.OnSellReward as RandomTaggedCardOnSellDefinition;

        return chestDefinition.InitialLevel == 3
            && !chestDefinition.SupportsLevel(2)
            && chestDefinition.SupportsLevel(3)
            && chestDefinition.SupportsLevel(4)
            && !chestDefinition.SupportsLevel(5)
            && chestDefinition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && chestDefinition.Attributes.Identity.Size == CardSize.Medium
            && chestDefinition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && reward?.Count == 3
            && reward.RequiredSize == CardSize.Small
            && reward.SameLevel
            && sale.IsSuccess
            && session.Player.Inventory.Cards.Count == 3
            && session.Player.Inventory.Cards.All(card =>
                card.Tags.Contains(GameTags.Material)
                && card.Attributes.Identity.Size == CardSize.Small
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4)
            && session.Board.Battlefield.Count == 3;
    }

    internal static bool CheckPurchase()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 10);
        var offer = ShopOffer.Create(new EconomyCardDefinition(CardSize.Medium));
        var result = economy.BuyCard(session, offer);
        var repeated = economy.BuyCard(session, offer);

        return result.IsSuccess
            && offer.Price == 4
            && session.Player.Wealth == 6
            && session.Player.Inventory.Cards.Count == 1
            && result.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 2
            && repeated.IsFailure
            && offer.IsSold;
    }

    internal static bool CheckInsufficientWealth()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 1);
        var result = economy.BuyCard(session, ShopOffer.Create(new EconomyCardDefinition(CardSize.Medium)));

        return result.IsFailure && session.Player.Wealth == 1 && session.Player.Inventory.Cards.Count == 0;
    }

    internal static bool CheckSale()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 3);
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Drop).Value!;
        card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 7);

        var firstSale = economy.SellCard(session, card.Id);
        var secondSale = economy.SellCard(session, card.Id);
        return firstSale.IsSuccess
            && firstSale.Value == 7
            && session.Player.Wealth == 10
            && session.Player.Inventory.Cards.Count == 0
            && secondSale.IsFailure;
    }

    internal static bool CheckMergeValueCompensation()
    {
        foreach (var size in new[] { CardSize.Small, CardSize.Medium, CardSize.Large })
        {
            var economy = new CardEconomyService(new EntityFactory());
            var session = new MatchSession(42);
            var definition = new EconomyCardDefinition(size);
            var card = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
            var growth = new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 3);
            card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, (int)size + 5);
            card.Attributes.Persistent.ApplyModifier(growth);
            var merged = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Drop);
            if (merged.IsFailure || merged.Value!.Id != card.Id || merged.Value.CurrentLevel != 2
                || card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != 2 * (int)size + 8) return false;
            card.Attributes.Persistent.RemoveModifier(growth.Id);
            if (card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != 2 * (int)size + 5) return false;
            economy.AcquireCard(session, definition, 2, CardAcquisitionSource.Reward);
            economy.AcquireCard(session, definition, 3, CardAcquisitionSource.Reward);
            if (card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != 8 * (int)size + 5) return false;
        }
        var hideDefinition = new BeastHideCardDefinition();
        var hideEconomy = new CardEconomyService(new EntityFactory());
        var hideSession = new MatchSession(43);
        var hide = hideEconomy.AcquireCard(hideSession, hideDefinition, 1, CardAcquisitionSource.Reward).Value!.Card;
        hide.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 8);
        hideEconomy.AcquireCard(hideSession, hideDefinition, 1, CardAcquisitionSource.Reward);
        if (hide.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != 11) return false;
        var board = CreateBoardService();
        var economyChain = new CardEconomyService(new EntityFactory(), board);
        var chain = new MatchSession(44, 100);
        var small = new EconomyCardDefinition(CardSize.Small);
        var first = economyChain.AcquireCard(chain, small, 1, CardAcquisitionSource.Reward).Value!.Card;
        var second = economyChain.AcquireCard(chain, small, 2, CardAcquisitionSource.Reward).Value!.Card;
        var highest = economyChain.AcquireCard(chain, small, 3, CardAcquisitionSource.Reward).Value!.Card;
        first.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 100);
        highest.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 24);
        board.PlaceCard(chain, highest.Id, BoardZone.Battlefield, 3);
        var result = economyChain.AcquireCard(chain, small, 1, CardAcquisitionSource.Reward);
        if (result.IsFailure || result.Value!.Id != highest.Id || result.Value.CurrentLevel != 4
            || highest.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != 28
            || chain.Board.Locate(highest.Id)?.Placement.Start != 3 || chain.Player.Inventory.Cards.Count != 1) return false;
        var sold = economyChain.SellFromBoard(chain, highest.Id);
        if (sold.IsFailure || sold.Value != 28 || chain.Player.Wealth != 128) return false;
        // 连续合并溢出在任何材料移除、扣费或升级之前拒绝。
        var overflow = new MatchSession(45, 100);
        var materials = new[] { 1, 2, 3 }.Select(level => economyChain.AcquireCard(overflow, small, level,
            CardAcquisitionSource.Reward).Value!.Card).ToArray();
        for (var index = 0; index < materials.Length; index++) board.PlaceCard(overflow, materials[index].Id, BoardZone.Battlefield, index);
        materials[2].Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, int.MaxValue);
        var offer = ShopOffer.Create(small, 1);
        return economyChain.CheckPurchase(overflow, offer).IsFailure
            && economyChain.BuyAndPlace(overflow, offer).IsFailure
            && economyChain.BuyCard(overflow, offer).IsFailure
            && economyChain.AcquireCard(overflow, small, 1, CardAcquisitionSource.Reward).IsFailure
            && overflow.Player.Wealth == 100 && !offer.IsSold && overflow.Board.Battlefield.Count == 3
            && overflow.Player.Inventory.Cards.Count == 3 && materials[2].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && materials[2].Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) == int.MaxValue;
    }


}
