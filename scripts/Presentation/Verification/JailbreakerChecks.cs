using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Monsters;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;

namespace Project_Star.Presentation.Verification;

// 越狱者的固定卡组、排程、战斗与奖励验证（表现层）。
internal static class JailbreakerChecks
{
    internal static bool DefinitionAndBattle()
    {
        var definition = new JailbreakerMonsterDefinition();
        var registry = DefinitionRegistry.Scan(typeof(JailbreakerMonsterDefinition).Assembly);
        var identity = definition.Attributes.Identity;
        var values = definition.Attributes.BaseCombat;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Monsters.ContainsKey(identity.Key) || identity.DisplayName != "越狱者"
            || definition.Level != 1 || definition.MinimumRound != 1 || definition.MaximumRound != 99
            || values.GetBaseValue(GameAttributeKeys.MaxHealth) != 60 || values.GetBaseValue(GameAttributeKeys.Armor) != 0
            || values.GetBaseValue(GameAttributeKeys.MaxMana) != 100 || values.GetBaseValue(GameAttributeKeys.Mana) != 0
            || values.GetBaseValue(GameAttributeKeys.ManaRegen) != 10 || values.GetBaseValue(GameAttributeKeys.HealthRegen) != 0
            || !definition.Skills.SequenceEqual(new[] { new MonsterSkillEntry(new StringName("skill.banish"), 2) })
            || !definition.Cards.SequenceEqual(new[]
            {
                new MonsterCardEntry(new StringName("card.lockpick"), 2, 0),
                new MonsterCardEntry(new StringName("card.oathbreaker"), 1, 1),
            }) || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var session = new CreateMatchService(factory).Create(1, 0, new PaladinHeroDefinition());
        session.Progress.Turn = 4;
        var choices = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true).Generate(session).Value!;
        var choice = choices.Single(item => item.Key == identity.Key);
        if (choice.Kind != EncounterKind.Monster || choice.Level != 1 || choice.Illustration != identity.Illustration
            || MatchSnapshot.From(session).EncounterChoices.Single(item => item.Key == identity.Key).Illustration != identity.Illustration)
            return false;
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        var independent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        var playerCard = factory.CreateCard(new FlangedMaceCardDefinition()); session.Player.Inventory.Add(playerCard);
        if (board.PlaceCard(session, playerCard.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var setup = new BattleSetupFactory().Create(session, opponent, 3, new BattleTick(41));
        var lockpick = opponent.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == new StringName("card.lockpick"));
        var oathbreaker = opponent.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == new StringName("card.oathbreaker"));
        var battle = new CombatSimulator().Simulate(setup);
        return setup.Opponent.Hero.MaxHealth == 60 && setup.Opponent.Hero.Level == 1
            && setup.Opponent.Cards.Count == 2 && setup.Opponent.Skills.Count == 1
            && opponent.Player.Skills.Items.Single().Attributes.Identity.Key == new StringName("skill.banish")
            && opponent.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2
            && opponent.Board.Battlefield.Placements.Single(item => item.CardId == lockpick.Id).Start == 0
            && opponent.Board.Battlefield.Placements.Single(item => item.CardId == oathbreaker.Id) is { Start: 1, Size: 2 }
            && lockpick.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2
            && oathbreaker.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && independent.Player.Inventory.Cards.All(card => opponent.Player.Inventory.Find(card.Id) is null)
            && battle.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == oathbreaker.Id && item.Tick.Value is 30 or 40)
            && battle.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == opponent.Player.Skills.Items.Single().Id
                && item.Tick.Value == 0 && item.SourceKind == AbilitySourceKind.Skill) == 1
            && battle.States.Any(frame => frame.Tick.Value == 0
                && frame.Cards.Count(card => card.Side == SideId.Player && card.Immobilize == 10) == 1
                && frame.Cards.Count(card => card.Side == SideId.Opponent && card.Immobilize == 10) == 1)
            && !battle.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == lockpick.Id)
            && battle.States.Any(frame => frame.Cards.Any(card => card.Id == playerCard.Id && card.Slow == 10));
    }

    internal static bool Rewards()
    {
        var registry = DefinitionRegistry.Scan(typeof(JailbreakerMonsterDefinition).Assembly);
        var definition = registry.Monsters[new StringName("monster.jailbreaker")];
        var seen = new System.Collections.Generic.HashSet<StringName>();
        foreach (var seed in Enumerable.Range(1, 16))
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var session = new CreateMatchService(factory).Create((ulong)seed, 0, new PaladinHeroDefinition());
            var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
            var victory = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
                new BattleTick(1), 200, 0, System.Array.Empty<BattleEvent>());
            var reputation = session.Player.Reputation;
            var wealth = session.Player.Wealth; var experience = session.Player.Experience;
            if (new MatchResultService(board).Apply(session, victory, MatchBattleKind.Monster, opponent: opponent).IsFailure
                || session.Player.Wealth != wealth + 3 || session.Player.Experience != experience + 2 || session.Player.Reputation != reputation
                || session.PendingMonsterRewards.Count != 1) return false;
            var reward = session.PendingMonsterRewards.Single();
            if (!(reward.Kind == MonsterRewardKind.Card && (reward.Key == new StringName("card.lockpick") && reward.Level == 2
                    || reward.Key == new StringName("card.oathbreaker") && reward.Level == 1)
                || reward.Kind == MonsterRewardKind.Skill && reward.Key == new StringName("skill.banish") && reward.Level == 2))
                return false;
            seen.Add(reward.Key);
            var claim = new MonsterRewardClaimService(registry, new CardEconomyService(factory, board),
                new SkillAcquisitionService(factory), board);
            if (claim.ClaimFirst(session).IsFailure || claim.ClaimFirst(session).IsSuccess
                || session.PendingMonsterRewards.Count != 0)
                return false;
            if (reward.Kind == MonsterRewardKind.Card)
            {
                if (session.Board.Battlefield.Count != 1 || session.Player.Skills.Items.Count != 0
                    || session.Player.Inventory.Cards.Single().Attributes.Identity.Key != reward.Key
                    || session.Player.Inventory.Cards.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != reward.Level)
                    return false;
            }
            else if (session.Board.Battlefield.Count != 0 || session.Player.Inventory.Cards.Count != 0
                || session.Player.Skills.Items.Single().Attributes.Identity.Key != reward.Key
                || session.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2)
                return false;
        }
        return seen.Count == 3;
    }
}
