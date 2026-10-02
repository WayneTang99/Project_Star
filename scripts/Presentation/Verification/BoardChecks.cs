using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 棋盘、套装与任务规则验证（表现层验证模块）。
internal static class BoardChecks
{
    internal static bool CheckDirectPlacement()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Medium);
        var result = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 3);
        var placement = session.Board.Battlefield.Find(card.Id);
        return result.IsSuccess && placement?.Start == 3 && placement.Size == 2;
    }

    internal static bool CheckPushDirections()
    {
        var service = CreateBoardService();
        var rightSession = new MatchSession(1);
        var rightBlocker = AcquireBoardCard(rightSession, CardSize.Small);
        var rightMover = AcquireBoardCard(rightSession, CardSize.Small);
        _ = service.PlaceCard(rightSession, rightBlocker.Id, BoardZone.Battlefield, 0);
        var right = service.PlaceCard(rightSession, rightMover.Id, BoardZone.Battlefield, 0);

        var leftSession = new MatchSession(2);
        var first = AcquireBoardCard(leftSession, CardSize.Small);
        var second = AcquireBoardCard(leftSession, CardSize.Small);
        var leftMover = AcquireBoardCard(leftSession, CardSize.Small);
        _ = service.PlaceCard(leftSession, first.Id, BoardZone.Battlefield, 4);
        _ = service.PlaceCard(leftSession, second.Id, BoardZone.Battlefield, 5);
        var left = service.PlaceCard(leftSession, leftMover.Id, BoardZone.Battlefield, 4);

        return right.Value?.Direction == PushDirection.Right
            && rightSession.Board.Battlefield.Find(rightBlocker.Id)?.Start == 1
            && left.Value?.Direction == PushDirection.Left
            && leftSession.Board.Battlefield.Find(first.Id)?.Start == 3;
    }

    internal static bool CheckRightTieBreak()
    {
        var session = new MatchSession(1);
        var blocker = AcquireBoardCard(session, CardSize.Small);
        var mover = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Battlefield, 4);
        var result = service.PlaceCard(session, mover.Id, BoardZone.Battlefield, 4);
        return result.Value?.Direction == PushDirection.Right
            && result.Value.TotalDistance == 1
            && result.Value.AffectedCards == 1
            && session.Board.Battlefield.Find(blocker.Id)?.Start == 5;
    }

    internal static bool CheckUnpushableCard()
    {
        var session = new MatchSession(1);
        var blocker = AcquireBoardCard(session, CardSize.Medium);
        var mover = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Battlefield, 4);
        _ = service.SetPushable(session, blocker.Id, false);
        var result = service.PlaceCard(session, mover.Id, BoardZone.Battlefield, 4);
        return result.IsFailure
            && session.Board.Battlefield.Find(blocker.Id)?.Start == 4
            && session.Board.Battlefield.Find(mover.Id) is null;
    }

    internal static bool CheckSameZoneMove()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Large);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 2);
        var result = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 3);
        return result.IsSuccess
            && session.Board.Battlefield.Count == 1
            && session.Board.Battlefield.Find(card.Id)?.Start == 3;
    }

    internal static bool CheckCrossZoneRollback()
    {
        var session = new MatchSession(1);
        var moving = AcquireBoardCard(session, CardSize.Medium);
        var blocker = AcquireBoardCard(session, CardSize.Medium);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, moving.Id, BoardZone.Battlefield, 2);
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Bench, 4);
        _ = service.SetPushable(session, blocker.Id, false);
        var result = service.PlaceCard(session, moving.Id, BoardZone.Bench, 4);

        return result.IsFailure
            && session.Board.Battlefield.Find(moving.Id)?.Start == 2
            && session.Board.Bench.Find(blocker.Id)?.Start == 4
            && session.Board.Bench.Find(moving.Id) is null;
    }

    internal static bool CheckUniqueBoardLocation()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 1);
        var moved = service.PlaceCard(session, card.Id, BoardZone.Bench, 7);
        return moved.IsSuccess
            && session.Board.Battlefield.Find(card.Id) is null
            && session.Board.Bench.Find(card.Id)?.Start == 7;
    }

    internal static bool CheckBoardCapacity()
    {
        var session = new MatchSession(1, boardCapacity: 10);
        var card = AcquireBoardCard(session, CardSize.Large);
        var result = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 8);
        return result.IsFailure && session.Board.Battlefield.Count == 0;
    }

    internal static bool CheckRemoveThenSell()
    {
        var session = new MatchSession(1);
        var economy = new CardEconomyService(new EntityFactory());
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Reward).Value!;
        var board = CreateBoardService();
        _ = board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var blockedSale = economy.SellCard(session, card.Id);
        var removed = board.RemoveFromBoard(session, card.Id);
        var sale = economy.SellCard(session, card.Id);
        return blockedSale.IsFailure && removed.IsSuccess && sale.IsSuccess;
    }

    internal static bool CheckCardSetBonuses()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationCardSetDefinition(),
            new VerificationSetCardDefinition("verification.set_card_a"),
            new VerificationSetCardDefinition("verification.set_card_b"),
            new VerificationSetCardDefinition("verification.set_card_c"),
        ]);
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var opponent = new MatchSession(2);
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var first = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var second = factory.CreateCard(registry.Cards[new StringName("verification.set_card_b")]);
        var duplicate = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var third = factory.CreateCard(registry.Cards[new StringName("verification.set_card_c")]);
        foreach (var card in new[] { first, second, duplicate, third })
            session.Player.Inventory.Add(card);
        if (board.PlaceCard(session, first.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Battlefield, 1).IsFailure
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || board.PlaceCard(session, duplicate.Id, BoardZone.Battlefield, 2).IsFailure
            || duplicate.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || board.PlaceCard(session, third.Id, BoardZone.Battlefield, 3).IsFailure)
            return false;

        var active = new CardSetEvaluator().Evaluate(session, registry.Sets);
        var frozen = new BattleSetupFactory(registry.Sets).Create(session, opponent, 1, new BattleTick(10));
        if (active.Count != 2
            || active[0].Threshold.RequiredDistinctCards != 2
            || active[1].Threshold.RequiredDistinctCards != 3
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 65
            || frozen.Player.Cards[0].AttackDamage != 65)
            return false;

        new CardSetBonusService(registry.Sets).Recalculate(session);
        if (first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 65
            || board.PlaceCard(session, third.Id, BoardZone.Bench, 0).IsFailure
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || third.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 5
            || frozen.Player.Cards[0].AttackDamage != 65)
            return false;

        var independent = new StatModifier(ModifierId.New(), first.Id, GameAttributeKeys.AttackDamage, 7);
        first.Attributes.BaseCombat.ApplyModifier(independent);
        return board.RemoveFromBoard(session, second.Id).IsSuccess
            && new CardSetEvaluator().Evaluate(session, registry.Sets).Count == 0
            && first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 12
            && duplicate.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5;
    }

    internal static bool CheckCardSetExplicitTargets()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationCardSetDefinition(explicitTargets: true),
            new VerificationSetCardDefinition("verification.set_card_a"),
            new BeastHideCardDefinition(),
        ]);
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var member = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var other = factory.CreateCard(registry.Cards[new StringName("card.beast_hide")]);
        var bench = factory.CreateCard(registry.Cards[new StringName("card.beast_hide")]);
        foreach (var card in new[] { member, other, bench }) session.Player.Inventory.Add(card);
        return board.PlaceCard(session, member.Id, BoardZone.Battlefield, 0).IsSuccess
            && board.PlaceCard(session, other.Id, BoardZone.Battlefield, 1).IsSuccess
            && board.PlaceCard(session, bench.Id, BoardZone.Bench, 0).IsSuccess
            && member.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 15
            && other.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 10
            && bench.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 0
            && session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 3;
    }

    internal static bool CheckCardSetBattleSource()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationBattleSetDefinition(), new VerificationSelfDestroyingSetCardDefinition(),
        ]);
        var factory = new EntityFactory();
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var card = factory.CreateCard(registry.Cards[new StringName("verification.self_destroying_set_card")]);
        player.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        if (board.PlaceCard(player, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var setup = new BattleSetupFactory(registry.Sets).Create(
            player, opponent, 3, new BattleTick(2), new BattleTick(300));
        if (setup.Player.Sets.Count != 1
            || board.PlaceCard(player, card.Id, BoardZone.Bench, 0).IsFailure)
            return false;
        var events = new CombatSimulator().Simulate(setup).Events;
        var destroyedAt = events.ToList().FindIndex(value => value is CardDestroyedEvent);
        var setActivatedAt = events.ToList().FindIndex(value => value is AbilityActivatedEvent
            { SourceKind: AbilitySourceKind.CardSet });
        return player.Board.Battlefield.Count == 0
            && destroyedAt >= 0 && setActivatedAt > destroyedAt
            && events.OfType<DamageDealtEvent>().Any(value =>
                value.SourceKind == DamageSourceKind.CardSet && value.RawDamage == 7)
            && events.OfType<AbilityActivatedEvent>().Count(value =>
                value.SourceKind == AbilitySourceKind.CardSet) == 1;
    }

    internal static bool CheckCardQuestProgress()
    {
        var definition = new VerificationQuestCardDefinition();
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver());
        var first = factory.CreateCard(definition);
        var second = factory.CreateCard(definition);
        var third = factory.CreateCard(definition);
        session.Player.Inventory.Add(first);
        session.Player.Inventory.Add(second);
        if (board.PlaceCard(session, first.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Bench, 0).IsFailure)
            return false;
        var quest = definition.Quests[0];
        if (first.IsQuestUnlocked(quest) || second.IsQuestUnlocked(quest)) return false;

        var victory = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
            BattleTick.Zero, 100, 0, []);
        if (new MatchResultService(board).Apply(session, victory, MatchBattleKind.Pvp).IsFailure
            || !first.IsQuestUnlocked(quest) || !second.IsQuestUnlocked(quest)
            || first.GetQuestProgress(quest.Key) != 1 || second.GetQuestProgress(quest.Key) != 1
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 105
            || second.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 5)
            return false;

        session.Player.Inventory.Add(third);
        if (board.PlaceCard(session, third.Id, BoardZone.Bench, 1).IsFailure) return false;
        var defeat = new BattleResult(BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated,
            BattleTick.Zero, 0, 100, []);
        if (new MatchResultService(board).Apply(session, defeat, MatchBattleKind.Pvp).IsFailure
            || third.GetQuestProgress(quest.Key) != 0
            || board.PlaceCard(session, first.Id, BoardZone.Bench, 2).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Battlefield, 0).IsFailure)
            return false;

        var opponent = new MatchSession(2);
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(2));
        var snapshot = MatchSnapshot.From(session);
        return first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5
            && second.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 105
            && frozen.Player.Cards[0].AttackDamage == 105
            && snapshot.Cards.Single(value => value.Id == first.Id).Quests[0].Unlocked
            && !snapshot.Cards.Single(value => value.Id == third.Id).Quests[0].Unlocked
            && new MatchResultService(board).Apply(session, victory, MatchBattleKind.Pvp).IsSuccess
            && third.IsQuestUnlocked(quest)
            && third.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5;
    }

    internal static bool CheckCardQuestBattleDestroy()
    {
        var definition = new VerificationQuestSelfDestroyCardDefinition();
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        var opponent = new MatchSession(2);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var card = factory.CreateCard(definition);
        session.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver());
        if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var setupFactory = new BattleSetupFactory();
        if (setupFactory.Create(session, opponent, 1, new BattleTick(2)).Player.Cards[0].Abilities!.Count != 1)
            return false;
        new CardQuestService(board).ProcessEvent(session, new BattleWonQuestEvent());
        var setup = setupFactory.Create(session, opponent, 1, new BattleTick(2));
        if (setup.Player.Cards[0].Abilities!.Count != 2) return false;
        var events = new CombatSimulator().Simulate(setup).Events;
        return events.OfType<CardDestroyedEvent>().Any(value => value.CardId == card.Id)
            && events.OfType<DamageDealtEvent>().Count(value => value.SourceCardId == card.Id) == 1
            && !events.OfType<AbilityActivatedEvent>().Any(value => value.SourceCardId == card.Id && value.IsEcho);
    }


}
