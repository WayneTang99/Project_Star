using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Content.Monsters;
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 遭遇、奖励与对局结算验证（表现层验证模块）。
internal static class EncounterChecks
{
    internal static bool CheckBoarMonsterDefinition()
    {
        var definition = new BoarMonsterDefinition();
        var registry = DefinitionRegistry.Create([definition, new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var session = new MatchSession(1);
        session.Progress.Turn = 4;
        var choices = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true).Generate(session).Value!;
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        return registry.Monsters.ContainsKey(new StringName("monster.boar"))
            && definition.Attributes.Identity.DisplayName == "野猪"
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) == 100
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Armor) == 0
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxMana) == 100
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Mana) == 0
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.ManaRegen) == 10
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.HealthRegen) == 0
            && definition.Level == 1
            && definition.Skills.Count == 1
            && definition.Skills[0].SkillKey == new StringName("skill.charge")
            && definition.Skills[0].Level == 1
            && definition.Cards.Count == 3
            && definition.Cards[0].CardKey == new StringName("card.beast_hide")
            && definition.Cards[0].Level == 1
            && definition.Cards[0].BoardStart == 0
            && definition.Cards[1].CardKey == new StringName("card.beast_hide")
            && definition.Cards[1].Level == 1
            && definition.Cards[1].BoardStart == 1
            && definition.Cards[2].CardKey == new StringName("card.boar")
            && definition.Cards[2].Level == 1
            && definition.Cards[2].BoardStart == 2
            && choices.Count == 1
            && choices[0].Key == new StringName("monster.boar")
            && choices[0].Kind == EncounterKind.Monster
            && opponent.Player.Hero!.Attributes.Identity.DisplayName == "野猪"
            && opponent.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards.Count == 3
            && opponent.Player.Skills.Items.Count == 1
            && opponent.Player.Skills.Items[0].Attributes.Identity.Key == new StringName("skill.charge")
            && opponent.Player.Skills.Items[0].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Board.Battlefield.Count == 3
            && opponent.Player.Inventory.Cards[0].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards[1].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards[2].Attributes.Identity.Key == new StringName("card.boar")
            && opponent.Player.Inventory.Cards[2].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Board.Battlefield.Placements[2].Start == 2
            && opponent.Board.Battlefield.Placements[2].Size == 2;
    }

    internal static bool CheckTrainingGround()
    {
        var definition = new TrainingGroundEncounterDefinition();
        var registry = DefinitionRegistry.Create([new PaladinHeroDefinition(), definition]);
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var resolver = new ResolveEncounterOptionService(factory, CreateBoardService(), Array.Empty<CardDefinition>());
        var heroDefinition = new PaladinHeroDefinition();

        var healthSession = matches.Create(1, 0, heroDefinition);
        healthSession.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 3);
        var healthOptions = resolver.CreateOptionSet(healthSession, definition);
        var healthResult = resolver.Resolve(healthSession, healthOptions, definition.Options[0].Key);
        var healthOpponent = matches.Create(2, 0, heroDefinition);
        var healthSetup = new BattleSetupFactory().Create(healthSession, healthOpponent, 1, new BattleTick(1));

        var trainingSession = matches.Create(3, 0, heroDefinition);
        var attackDefinition = new AttackValueVerificationCardDefinition();
        var thornDefinition = new ThornArmorCardDefinition();
        var noAttackDefinition = new BeastHideCardDefinition();
        var cardEconomy = new CardEconomyService(factory);
        var attackCard = cardEconomy.AcquireCard(trainingSession, attackDefinition, 1, CardAcquisitionSource.Reward).Value!.Card;
        var thornCard = cardEconomy.AcquireCard(trainingSession, thornDefinition, 1, CardAcquisitionSource.Reward).Value!.Card;
        var noAttackCard = cardEconomy.AcquireCard(trainingSession, noAttackDefinition, 1, CardAcquisitionSource.Reward).Value!.Card;
        var board = CreateBoardService();
        _ = board.PlaceCard(trainingSession, attackCard.Id, BoardZone.Battlefield, 0);
        _ = board.PlaceCard(trainingSession, thornCard.Id, BoardZone.Battlefield, 1);
        _ = board.PlaceCard(trainingSession, noAttackCard.Id, BoardZone.Battlefield, 3);
        var trainingOptions = resolver.CreateOptionSet(trainingSession, definition);
        var trainingResult = resolver.Resolve(trainingSession, trainingOptions, definition.Options[1].Key);
        var invalidResult = resolver.Resolve(
            trainingSession,
            trainingOptions,
            new StringName("encounter.training_ground.unknown"));

        return registry.Encounters.ContainsKey(new StringName("encounter.training_ground"))
            && definition.Kind == EncounterKind.Other
            && definition.Attributes.Identity.DisplayName == "校场"
            && definition.MinimumRound == 1
            && definition.MaximumRound == 99
            && definition.Options.Count == 2
            && definition.Options[0].Key == new StringName("encounter.training_ground.body_training")
            && definition.Options[0].DisplayName == "体能训练"
            && definition.Options[1].Key == new StringName("encounter.training_ground.sparring")
            && definition.Options[1].DisplayName == "对阵训练"
            && healthSession.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && healthResult.IsSuccess
            && healthResult.Value!.Changes.Count == 1
            && healthResult.Value.Changes[0].Amount == 30
            && healthSetup.Player.Hero.MaxHealth == 230
            && trainingResult.IsSuccess
            && trainingResult.Value!.CardChanges!.Count == 1
            && trainingResult.Value.CardChanges[0].CardCount == 2
            && attackCard.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 35
            && thornCard.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 5
            && noAttackCard.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 0
            && invalidResult.IsFailure;
    }

    internal static bool CheckLandfill()
    {
        var definition = new LandfillEncounterDefinition();
        var heroDefinition = new PaladinHeroDefinition();
        CardDefinition[] cards = [new ArmguardCardDefinition(), new MilitaryBootsCardDefinition(), new BeastHideCardDefinition()];
        var registryItems = new List<object> { heroDefinition, definition };
        registryItems.AddRange(cards.Cast<object>());
        var registry = DefinitionRegistry.Create(registryItems);
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var board = CreateBoardService();
        var resolver = new ResolveEncounterOptionService(factory, board, cards);
        var wealthKey = new StringName("encounter.landfill.wealth");
        var materialKey = new StringName("encounter.landfill.material_small_card");

        var deterministicA = resolver.CreateOptionSet(matches.Create(77, 0, heroDefinition), definition);
        var deterministicB = resolver.CreateOptionSet(matches.Create(77, 0, heroDefinition), definition);
        if (deterministicA.Options.Count != 2
            || deterministicA.Options[0].Key != wealthKey
            || deterministicA.Options[1].Key != materialKey
            || deterministicA.Options[1].Key != deterministicB.Options[1].Key)
        {
            return false;
        }

        var wealthSession = matches.Create(88, 0, heroDefinition);
        var wealthOptions = resolver.CreateOptionSet(wealthSession, definition);
        var wealthResult = resolver.Resolve(wealthSession, wealthOptions, wealthKey);
        var repeatedResult = resolver.Resolve(wealthSession, wealthOptions, wealthKey);
        var materialSession = matches.Create(89, 0, heroDefinition);
        var materialOptions = resolver.CreateOptionSet(materialSession, definition);
        var materialResult = resolver.Resolve(materialSession, materialOptions, materialKey);

        var fullSession = matches.Create(99, 0, heroDefinition);
        var economy = new CardEconomyService(factory);
        for (var index = 0; index < 20; index++)
        {
            var blocker = economy.AcquireCard(fullSession, cards[2], 4, CardAcquisitionSource.Reward).Value!;
            var zone = index < 10 ? BoardZone.Battlefield : BoardZone.Bench;
            _ = board.PlaceCard(fullSession, blocker.Id, zone, index % 10);
        }
        var fullCardOptions = resolver.CreateOptionSet(fullSession, definition);
        var inventoryBefore = fullSession.Player.Inventory.Cards.Count;
        var fullResult = resolver.Resolve(fullSession, fullCardOptions, materialKey);

        return registry.Encounters.ContainsKey(new StringName("encounter.landfill"))
            && definition.Attributes.Identity.DisplayName == "垃圾场"
            && definition.Level == 1
            && definition.Options.Count == 2
            && definition.OptionSlots.Count == 2
            && definition.Options[0].DisplayName == "拾取零钱"
            && definition.Options[1].DisplayName == "变废为宝"
            && definition.OptionSlots[1].Candidates.Count == 1
            && wealthResult.IsSuccess
            && wealthResult.Value!.WealthGained == 2
            && wealthSession.Player.Wealth == 7
            && repeatedResult.IsFailure
            && materialResult.Value?.GrantedCard?.Tags.Contains(GameTags.Material) == true
            && materialResult.Value.GrantedCard.Attributes.Identity.Size == CardSize.Small
            && fullResult.IsSuccess
            && fullResult.Value!.CardRewardSkipped
            && fullResult.Value.GrantedCard is null
            && fullSession.Player.Inventory.Cards.Count == inventoryBefore;
    }

    internal static bool CheckTavernEncounter()
    {
        var definition = new TavernEncounterDefinition();
        var heroDefinition = new PaladinHeroDefinition();
        var otherFactionCard = new ShopVerificationCardDefinition(
            CardSize.Small,
            new StringName("other_hero"),
            "tavern");
        var registry = DefinitionRegistry.Create([heroDefinition, definition]);
        var cards = new CardDefinition[] { otherFactionCard };
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var board = CreateBoardService();
        var resolver = new ResolveEncounterOptionService(factory, board, cards);
        var tradeKey = new StringName("encounter.tavern.trade");
        var cheersKey = new StringName("encounter.tavern.cheers");
        var tradeSession = matches.Create(311, 0, heroDefinition);
        var tradeOptions = resolver.CreateOptionSet(tradeSession, definition);
        var tradeResult = resolver.Resolve(tradeSession, tradeOptions, tradeKey);

        var insufficientSession = matches.Create(312, 0, heroDefinition);
        insufficientSession.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 1);
        var insufficientOptions = resolver.CreateOptionSet(insufficientSession, definition);
        var insufficientResult = resolver.Resolve(insufficientSession, insufficientOptions, tradeKey);

        var cheerSession = matches.Create(313, 0, heroDefinition);
        cheerSession.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 3);
        var cheerOptions = resolver.CreateOptionSet(cheerSession, definition);
        var cheerResult = resolver.Resolve(cheerSession, cheerOptions, cheersKey);
        var opponent = matches.Create(314, 0, heroDefinition);
        var setupFactory = new BattleSetupFactory();
        var boostedSetup = setupFactory.Create(cheerSession, opponent, 1, new BattleTick(2));
        var battleResult = new StartBattleService(setupFactory, new CombatSimulator())
            .StartBattle(cheerSession, opponent, 2);
        var nextBattleSetup = setupFactory.Create(cheerSession, opponent, 3, new BattleTick(2));

        return registry.Encounters.ContainsKey(new StringName("encounter.tavern"))
            && definition.Level == 1
            && definition.MinimumRound == 1
            && definition.MaximumRound == 3
            && definition.Options.Count == 2
            && definition.Options[0].Key == tradeKey
            && definition.Options[1].Key == cheersKey
            && tradeResult.IsSuccess
            && tradeResult.Value!.WealthSpent == 2
            && tradeResult.Value.GrantedCard?.Attributes.Identity.FactionKey == new StringName("other_hero")
            && tradeResult.Value.GrantedCard.Attributes.Identity.Size == CardSize.Small
            && tradeResult.Value.GrantedCard.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && tradeSession.Player.Wealth == 3
            && tradeSession.Board.Battlefield.Placements.Count + tradeSession.Board.Bench.Placements.Count == 1
            && insufficientResult.IsFailure
            && insufficientSession.Player.Wealth == 1
            && insufficientSession.Player.Inventory.Cards.Count == 0
            && cheerResult.IsSuccess
            && cheerResult.Value!.PendingBattleMaxHealthBonus == 30
            && boostedSetup.Player.Hero.MaxHealth == 230
            && battleResult.IsSuccess
            && cheerSession.PendingBattleMaxHealthBonus == 0
            && nextBattleSetup.Player.Hero.MaxHealth == 200;
    }

    internal static bool CheckNormalEncounterChoices()
    {
        var session = new MatchSession(10);
        var result = CreateEncounterScheduler().Generate(session);
        var hasShop = false;
        var keys = new HashSet<StringName>();
        foreach (var choice in result.Value!)
        {
            hasShop |= choice.Kind == EncounterKind.Shop;
            if (choice.Kind is EncounterKind.Monster or EncounterKind.Pvp) return false;
            keys.Add(choice.Key);
        }
        return result.IsSuccess && result.Value!.Count == 3 && hasShop && keys.Count == 3;
    }

    internal static bool CheckDeterministicEncounters()
    {
        var first = CreateEncounterScheduler().Generate(new MatchSession(77)).Value!;
        var second = CreateEncounterScheduler().Generate(new MatchSession(77)).Value!;
        if (first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++)
            if (first[index].Key != second[index].Key) return false;
        return true;
    }

    internal static bool CheckEncounterSeenHistory()
    {
        var session = new MatchSession(5);
        var choices = CreateEncounterScheduler().Generate(session).Value!;
        foreach (var choice in choices)
            if (!session.EncounterSchedule.SeenKeys.Contains(choice.Key)) return false;
        return true;
    }

    internal static bool CheckMonsterTurn()
    {
        var session = new MatchSession(6);
        session.Progress.Turn = 4;
        var choices = CreateEncounterScheduler().Generate(session).Value!;
        return choices.Count == 3
            && choices[0].Kind == EncounterKind.Monster
            && choices[1].Kind == EncounterKind.Monster
            && choices[2].Kind == EncounterKind.Monster;
    }

    internal static bool CheckPvpTurnAdvance()
    {
        var session = new MatchSession(8);
        session.Progress.Turn = 8;
        var scheduler = CreateEncounterScheduler();
        var choices = scheduler.Generate(session).Value!;
        var selected = scheduler.Select(session, choices[0].Key);
        return choices.Count == 1
            && choices[0].Kind == EncounterKind.Pvp
            && selected.Value?.RequiresBattle == true
            && session.Progress.Round == 2
            && session.Progress.Turn == 1;
    }

    internal static bool CheckEncounterHistoryIsolation()
    {
        var first = new MatchSession(1);
        var second = new MatchSession(1);
        _ = CreateEncounterScheduler().Generate(first);
        return first.EncounterSchedule.SeenKeys.Count > 0 && second.EncounterSchedule.SeenKeys.Count == 0;
    }

    internal static bool CheckMonsterLoss()
    {
        var session = new MatchSession(1);
        var battle = new BattleResult(BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated,
            new BattleTick(1), 0, 50, Array.Empty<BattleEvent>());
        _ = CreateMatchResultService().Apply(session, battle, MatchBattleKind.Monster,
            opponent: CreateBoarOpponent());
        return session.Player.Reputation == 10
            && session.Player.Wealth == 1
            && session.Player.Experience == 1
            && session.PendingMonsterRewards.Count == 0
            && session.Status == MatchStatus.InProgress;
    }

    internal static bool CheckMonsterReward()
    {
        var session = new MatchSession(1, 3);
        var opponent = CreateBoarOpponent();
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: opponent);
        var pending = session.PendingMonsterRewards.Count == 1 ? session.PendingMonsterRewards[0] : null;
        var repeated = new MatchSession(1, 3);
        _ = CreateMatchResultService().Apply(repeated, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: CreateBoarOpponent());
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        return session.Player.Wealth == 6
            && session.Player.Experience == 2
            && pending is { Kind: MonsterRewardKind.Card, Level: 1 }
            && (pending.Key == new StringName("card.beast_hide") || pending.Key == new StringName("card.boar"))
            && repeated.PendingMonsterRewards.Count == 1
            && repeated.PendingMonsterRewards[0].Key == pending.Key
            && claimed.IsSuccess
            && session.PendingMonsterRewards.Count == 0
            && session.Player.Inventory.Cards.Count == 1
            && session.Board.Battlefield.Count == 1;
    }

    internal static bool CheckMonsterSkillReward()
    {
        var factory = new EntityFactory();
        var skill = new AssaultSkillDefinition();
        var monster = new SkillOnlyVerificationMonsterDefinition();
        var registry = DefinitionRegistry.Create([monster, skill]);
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, monster);
        var session = new MatchSession(1);
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: opponent);
        var pending = session.PendingMonsterRewards.Count == 1 ? session.PendingMonsterRewards[0] : null;
        var board = CreateBoardService();
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        var genericPassed = pending is { Kind: MonsterRewardKind.Skill, Level: 1 }
            && pending.Key == new StringName("skill.assault")
            && opponent.Player.Skills.Items.Count == 1
            && claimed.IsSuccess
            && session.Player.Skills.Items.Count == 1
            && session.Board.Battlefield.Count == 0
            && session.PendingMonsterRewards.Count == 0;
        if (!genericPassed) return false;

        var boarRegistry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var boar = new LocalTestOpponentProvider(boarRegistry).CreateMonsterOpponent(
            2, boarRegistry.Monsters[new StringName("monster.boar")]);
        var boarSession = new MatchSession(7);
        _ = CreateMatchResultService().Apply(boarSession, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: boar);
        var boarPending = boarSession.PendingMonsterRewards.Count == 1
            ? boarSession.PendingMonsterRewards[0] : null;
        var boarBoard = CreateBoardService();
        var boarClaimed = new MonsterRewardClaimService(boarRegistry,
            new CardEconomyService(factory, boarBoard), new SkillAcquisitionService(factory), boarBoard)
            .ClaimFirst(boarSession);
        return boarPending is { Kind: MonsterRewardKind.Skill, Level: 1 }
            && boarPending.Key == new StringName("skill.charge")
            && boarClaimed.IsSuccess
            && boarSession.Player.Skills.Items.Count == 1
            && boarSession.Player.Skills.Items[0].Attributes.Identity.Key == new StringName("skill.charge");
    }

    internal static bool CheckMonsterRewardRetention()
    {
        var session = new MatchSession(1, boardCapacity: 1);
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var blockerDefinition = new BeastHideCardDefinition();
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var blocker = factory.CreateCard(blockerDefinition, 4);
            session.Player.Inventory.Add(blocker);
            if (board.PlaceCard(session, blocker.Id, zone, 0).IsFailure) return false;
        }
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: CreateBoarOpponent());
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), blockerDefinition, new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        return claimed.IsFailure
            && session.PendingMonsterRewards.Count == 1
            && session.Player.Inventory.Cards.Count == 2
            && session.Board.Battlefield.Count == 1
            && session.Board.Bench.Count == 1;
    }

    internal static bool CheckPvpReputationLoss()
    {
        var session = new MatchSession(1);
        session.Progress.Round = 3;
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.OpponentVictory), MatchBattleKind.Pvp, 3);
        return session.Player.Reputation == 7;
    }

    internal static bool CheckTenPvpWins()
    {
        var session = new MatchSession(1);
        var service = CreateMatchResultService();
        for (var index = 0; index < 10; index++)
            _ = service.Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory), MatchBattleKind.Pvp);
        return session.Status == MatchStatus.Won && session.Summary?.PvpWins == 10;
    }

    internal static bool CheckVictoryPriority()
    {
        var session = new MatchSession(1);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Reputation, 0);
        session.Progress.PvpWins = 9;
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory), MatchBattleKind.Pvp);
        return session.Status == MatchStatus.Won;
    }

    internal static bool CheckPermanentChangeApplication()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Small);
        _ = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var battle = new BattleResult(BattleOutcome.Draw, BattleEndReason.Timeout, new BattleTick(1), 1, 1,
            Array.Empty<BattleEvent>(), [new PermanentChange(card.Id, "Destroy")]);
        _ = CreateMatchResultService().Apply(session, battle, MatchBattleKind.Monster,
            opponent: CreateBoarOpponent());
        return session.Player.Inventory.Find(card.Id) is null && !session.Board.Contains(card.Id);
    }

    internal static bool CheckMatchSnapshot()
    {
        var session = new MatchSession(1, 4);
        _ = AcquireBoardCard(session, CardSize.Small);
        var snapshot = MatchSnapshot.From(session);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 99);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Experience, 42);
        return snapshot.Wealth == 4 && snapshot.Experience == 0 && snapshot.Cards.Count == 1;
    }


}
