using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 冒险者猎弓和套装的真实攻击、奖励、经验及动态生命验证（表现层）。
internal static class AdventurerHuntingBowChecks
{
    internal static bool IdentityAndDamage()
    {
        var definition = new AdventurerHuntingBowCardDefinition(); var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(RobinHeroDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || !registry.Sets.ContainsKey(identity.SetKey!)
            || identity.DisplayName != "冒险者猎弓" || identity.FactionKey != new StringName("robin")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 1 || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Equipment) || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var (level, damage) in new[] { (1, 40), (2, 80), (3, 160), (4, 320) })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
            var matches = new CreateMatchService(factory); var session = matches.Create(1, 100, new RobinHeroDefinition());
            var enemy = matches.Create(2, 0, new RobinHeroDefinition());
            enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1000);
            enemy.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 10);
            var bow = factory.CreateCard(definition, level); session.Player.Inventory.Add(bow);
            board.PlaceCard(session, bow.Id, BoardZone.Battlefield, 0);
            var setup = new BattleSetupFactory(registry.Sets).Create(session, enemy, 3, new BattleTick(60));
            var result = new CombatSimulator().Simulate(setup);
            var hit = result.Events.OfType<DamageDealtEvent>().Single();
            var reward = result.MatchRewardSources.Single().Abilities.Single().Effects.Single() as GrantSourceLevelCardEffectDefinition;
            if (hit.Tick.Value != 60 || hit.RawDamage != damage || hit.ArmorAbsorbed != 10 || hit.HealthDamage != damage - 10
                || result.Events.OfType<ManaChangedEvent>().Any() || reward?.Card.Attributes.Identity.Key != new StringName("card.beast_hide")
                || result.MatchRewardSources.Single().Level != level || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || CardDisplayAdapter.FaceEffects(MatchSnapshot.From(session).Cards.Single()).Single().Value != damage.ToString()) return false;
            var sets = MatchDisplayQuery.Capture(session, registry.Sets).Sets.Single(item => item.Key == new StringName("set.adventurer"));
            if (sets.DisplayName != "冒险者" || sets.DistinctCardCount != 1 || sets.Thresholds.Any(threshold => threshold.Active)
                || !sets.Thresholds.Select(threshold => threshold.RequiredCount).SequenceEqual(new[] { 2, 3, 5 })) return false;
        }
        return true;
    }

    internal static bool RewardsAndPlacement()
    {
        var sets = Sets(); var definition = new AdventurerHuntingBowCardDefinition();
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        foreach (var kind in new[] { MatchBattleKind.Monster, MatchBattleKind.Pvp })
        foreach (var outcome in new[] { BattleOutcome.PlayerVictory, BattleOutcome.OpponentVictory, BattleOutcome.Draw })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), sets);
            var matches = new CreateMatchService(factory); var session = matches.Create(1, 100, new RobinHeroDefinition());
            var enemy = matches.Create(2, 0, new RobinHeroDefinition());
            var bow = factory.CreateCard(definition, level); session.Player.Inventory.Add(bow); board.PlaceCard(session, bow.Id, zone, 0);
            var simulated = new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(session, enemy, 3, new BattleTick(1)));
            var result = Outcome(simulated, outcome); var service = new MatchResultService(board);
            if (level < 4) factory.ApplyCardLevel(bow, definition, level + 1);
            if (service.Apply(session, result, kind, opponent: enemy).IsFailure) return false;
            var hides = session.Player.Inventory.Cards.Where(card => card.Attributes.Identity.Key == new StringName("card.beast_hide")).ToArray();
            var shouldReward = zone == BoardZone.Battlefield && kind == MatchBattleKind.Monster && outcome == BattleOutcome.PlayerVictory;
            if (hides.Length != (shouldReward ? 1 : 0)) return false;
            if (shouldReward && (hides.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != level
                || !session.Board.Battlefield.Placements.Any(placement => placement.CardId == hides.Single().Id))) return false;
            MatchAbilityService.ApplyVictory(session, result, kind, board);
            if (session.Player.Inventory.Cards.Count(card => card.Attributes.Identity.Key == new StringName("card.beast_hide")) != hides.Length) return false;
        }
        var mergeFactory = new EntityFactory(); var mergeBoard = new BoardService(new BoardPlacementSolver(), sets);
        var match = new CreateMatchService(mergeFactory).Create(4, 0, new RobinHeroDefinition());
        var opponent = new CreateMatchService(mergeFactory).Create(5, 0, new RobinHeroDefinition());
        var source = mergeFactory.CreateCard(definition, 2); match.Player.Inventory.Add(source); mergeBoard.PlaceCard(match, source.Id, BoardZone.Battlefield, 0);
        var hide = new CardEconomyService(mergeFactory, mergeBoard).AcquireAndPlace(match, new BeastHideCardDefinition(), 2, CardAcquisitionSource.Reward).Value!.Card;
        var victory = Outcome(new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(match, opponent, 1, new BattleTick(1))), BattleOutcome.PlayerVictory);
        new MatchResultService(mergeBoard).Apply(match, victory, MatchBattleKind.Monster, opponent: opponent);
        if (hide.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3 || match.Player.Inventory.Cards.Count != 2) return false;
        var full = new MatchSession(7, boardCapacity: 2); full.Player.SelectHero(mergeFactory.CreateHero(new RobinHeroDefinition()));
        var fullBow = mergeFactory.CreateCard(definition, 3); full.Player.Inventory.Add(fullBow); mergeBoard.PlaceCard(full, fullBow.Id, BoardZone.Battlefield, 0);
        for (var index = 0; index < 2; index++)
        {
            var filler = mergeFactory.CreateCard(new MemberFixture(index, false)); full.Player.Inventory.Add(filler);
            mergeBoard.PlaceCard(full, filler.Id, BoardZone.Bench, index);
        }
        var fullBattle = Outcome(new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(full, opponent, 1, new BattleTick(1))), BattleOutcome.PlayerVictory);
        if (new MatchResultService(mergeBoard).Apply(full, fullBattle, MatchBattleKind.Monster, opponent: opponent).IsFailure
            || full.PendingMonsterRewards.Count != 1 || full.PendingMonsterRewards.Single().Level != 3) return false;
        return true;
    }

    internal static bool SetExperience()
    {
        var factory = new EntityFactory(); var sets = Sets(); var board = new BoardService(new BoardPlacementSolver(), sets);
        var matches = new CreateMatchService(factory); var match = matches.Create(1, 100, new RobinHeroDefinition());
        var enemy = matches.Create(2, 0, new RobinHeroDefinition());
        var first = Add(0, 0); var duplicate = Add(0, 1);
        var rounds = new RoundIncomeService(sets);
        match.Progress.Round = 2;
        if (rounds.SettleCurrentRound(match).IsFailure || match.Player.Experience != 1) return false;
        var second = Add(1, 2);
        if (rounds.SettleCurrentRound(match).IsFailure || match.Player.Experience != 1) return false;
        match.Progress.Round = 3;
        if (rounds.SettleCurrentRound(match).IsFailure || match.Player.Experience != 2
            || rounds.SettleCurrentRound(match).Value != 0 || match.Player.Experience != 2) return false;
        var third = Add(2, 3);
        foreach (var kind in new[] { MatchBattleKind.Pvp, MatchBattleKind.Monster })
        {
            var frozen = new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(match, enemy, 9, new BattleTick(1)));
            var before = match.Player.Experience;
            var victory = Outcome(frozen, BattleOutcome.PlayerVictory);
            MatchAbilityService.ApplyVictory(match, victory, kind, board);
            MatchAbilityService.ApplyVictory(match, victory, kind, board);
            if (match.Player.Experience != before + 1) return false;
            MatchAbilityService.ApplyVictory(match, Outcome(frozen, BattleOutcome.OpponentVictory), kind, board);
            MatchAbilityService.ApplyVictory(match, Outcome(frozen, BattleOutcome.Draw), kind, board);
            if (match.Player.Experience != before + 1) return false;
        }
        var captured = Outcome(new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(match, enemy, 9, new BattleTick(1))), BattleOutcome.PlayerVictory);
        board.PlaceCard(match, third.Id, BoardZone.Bench, 0);
        var prior = match.Player.Experience;
        MatchAbilityService.ApplyVictory(match, captured, MatchBattleKind.Pvp, board);
        if (match.Player.Experience != prior + 1) return false;
        var next = Outcome(new CombatSimulator().Simulate(new BattleSetupFactory(sets).Create(match, enemy, 9, new BattleTick(1))), BattleOutcome.PlayerVictory);
        MatchAbilityService.ApplyVictory(match, next, MatchBattleKind.Pvp, board);
        if (match.Player.Experience != prior + 1) return false;
        var snapshot = MatchDisplayQuery.Capture(match, sets).Sets.Single();
        return snapshot.DistinctCardCount == 2 && snapshot.Thresholds.Count(threshold => threshold.Active) == 1
            && CardDisplayAdapter.AbilityDetails(snapshot.Thresholds[0].Abilities).Contains("轮初");

        CardInstance Add(int index, int start)
        {
            var card = factory.CreateCard(new MemberFixture(index)); match.Player.Inventory.Add(card);
            if (board.PlaceCard(match, card.Id, BoardZone.Battlefield, start).IsFailure) throw new InvalidOperationException("套装验证放置失败。");
            return card;
        }
    }

    internal static bool DynamicHealth()
    {
        var factory = new EntityFactory(); var sets = Sets(); var board = new BoardService(new BoardPlacementSolver(), sets);
        var matches = new CreateMatchService(factory); var match = matches.Create(1, 100, new RobinHeroDefinition());
        var enemy = matches.Create(2, 0, new RobinHeroDefinition());
        var members = Enumerable.Range(0, 5).Select(index => factory.CreateCard(new MemberFixture(index))).ToArray();
        foreach (var (card, index) in members.Select((card, index) => (card, index)))
        { match.Player.Inventory.Add(card); board.PlaceCard(match, card.Id, BoardZone.Battlefield, index); }
        var health = match.Player.Hero!.Attributes.BaseCombat;
        if (health.GetFinalValue(GameAttributeKeys.MaxHealth) != 210) return false;
        var old = new BattleSetupFactory(sets).Create(match, enemy, 1, new BattleTick(1));
        match.Player.AddExperience(8); match.Progress.Turn = 2;
        if (new TurnExperienceService(sets).SettleCurrentTurn(match).IsFailure
            || match.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || health.GetFinalValue(GameAttributeKeys.MaxHealth) != 260 || old.Player.Hero.MaxHealth != 210) return false;
        board.RefreshBonuses(match); board.RefreshBonuses(match);
        if (health.GetFinalValue(GameAttributeKeys.MaxHealth) != 260) return false;
        board.PlaceCard(match, members[4].Id, BoardZone.Bench, 0);
        if (health.GetFinalValue(GameAttributeKeys.MaxHealth) != 240) return false;
        board.PlaceCard(match, members[4].Id, BoardZone.Battlefield, 4);
        if (health.GetFinalValue(GameAttributeKeys.MaxHealth) != 260) return false;
        if (new CardEconomyService(factory, board).SellFromBoard(match, members[4].Id).IsFailure
            || health.GetFinalValue(GameAttributeKeys.MaxHealth) != 240) return false;
        return MatchDisplayQuery.Capture(match, sets).Sets.Single().Thresholds.Count(threshold => threshold.Active) == 2;
    }

    private static Dictionary<StringName, CardSetDefinition> Sets() => new()
        { [new StringName("set.adventurer")] = new AdventurerSetDefinition() };

    private static BattleResult Outcome(BattleResult source, BattleOutcome outcome) => new(outcome,
        source.EndReason, source.EndedAt, source.PlayerRemainingHealth, source.OpponentRemainingHealth,
        source.Events, source.PermanentChanges, source.States, source.PermanentAttributeBonuses)
        { MatchRewardSources = source.MatchRewardSources };

    // 不同套装卡牌key与满盘占格的内部夹具，不加入正式内容（表现层验证）。
    private sealed class MemberFixture : CardDefinition
    {
        internal MemberFixture(int index, bool inSet = true) : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName($"verification.adventurer.member{index}"), "套装验证卡", new StringName("neutral"),
                CardSize.Small, [GameElements.General], setKey: inSet ? new StringName("set.adventurer") : null)), new TagSet()) { }
    }
}
