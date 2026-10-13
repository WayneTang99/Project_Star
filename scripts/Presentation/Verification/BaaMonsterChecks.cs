using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Content.Monsters;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;

namespace Project_Star.Presentation.Verification;

// 咩咩定义、遭遇投影、实战能力与卡牌奖励等级验证。
internal static class BaaMonsterChecks
{
    internal static bool DefinitionAndBattle()
    {
        var registry = DefinitionRegistry.Scan(typeof(BaaMonsterDefinition).Assembly);
        var definition = registry.Monsters[new StringName("monster.baa")];
        var identity = definition.Attributes.Identity;
        var values = definition.Attributes.BaseCombat;
        if (identity.DisplayName != "咩咩" || definition.Level != 1
            || definition.MinimumRound != 1 || definition.MaximumRound != 99
            || !definition.Skills.SequenceEqual(new[] { new MonsterSkillEntry(new StringName("skill.healing"), 1) })
            || values.GetBaseValue(GameAttributeKeys.MaxHealth) != 100 || values.GetBaseValue(GameAttributeKeys.Armor) != 0
            || values.GetBaseValue(GameAttributeKeys.MaxMana) != 100 || values.GetBaseValue(GameAttributeKeys.Mana) != 0
            || values.GetBaseValue(GameAttributeKeys.ManaRegen) != 10 || values.GetBaseValue(GameAttributeKeys.HealthRegen) != 0
            || !definition.Cards.SequenceEqual(new[]
            {
                new MonsterCardEntry(new StringName("card.silver_needle_grass"), 2, 0),
                new MonsterCardEntry(new StringName("card.baa_sheep"), 1, 1),
                new MonsterCardEntry(new StringName("card.windmill_daisy"), 1, 2),
            })) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        var session = new CreateMatchService(new EntityFactory()).Create(1, 0, new PaladinHeroDefinition());
        session.Progress.Turn = 4;
        var scheduler = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true);
        var choice = scheduler.Generate(session).Value!.Single(item => item.Key == identity.Key);
        if (choice.Kind != EncounterKind.Monster || choice.Level is < 1 or > 4 || choice.Illustration != identity.Illustration
            || MatchSnapshot.From(session).EncounterChoices.Single(item => item.Key == identity.Key).Illustration != identity.Illustration)
            return false;
        session.Progress.Round = 3;
        if (new EncounterScheduler(registry).Generate(session).Value?.Count != 3) return false;
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        var setup = new BattleSetupFactory().Create(session, opponent, 3, new BattleTick(100));
        var sheep = opponent.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == new StringName("card.baa_sheep"));
        var grass = opponent.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == new StringName("card.silver_needle_grass"));
        var flower = opponent.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == new StringName("card.windmill_daisy"));
        var battle = new CombatSimulator().Simulate(setup);
        return setup.Opponent.Hero.MaxHealth == 100 && setup.Opponent.Hero.Level == 1
            && setup.Opponent.Cards.Count == 3 && setup.Opponent.Skills.Count == 1
            && opponent.Player.Skills.Items.Single().Attributes.Identity.Key == new StringName("skill.healing")
            && opponent.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Board.Battlefield.Placements.Single(item => item.CardId == grass.Id) is { Start: 0, Size: 1 }
            && opponent.Board.Battlefield.Placements.Single(item => item.CardId == sheep.Id) is { Start: 1, Size: 1 }
            && opponent.Board.Battlefield.Placements.Single(item => item.CardId == flower.Id) is { Start: 2, Size: 1 }
            && flower.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && grass.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2
            && sheep.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && battle.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == sheep.Id && item.Tick.Value == 50)
            && battle.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == flower.Id && item.Tick.Value == 40)
            && !battle.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == grass.Id)
            && battle.States.Any(frame => frame.Cards.Any(card => card.Id == sheep.Id && card.Haste > 0));
    }

    internal static bool Rewards()
    {
        var registry = DefinitionRegistry.Scan(typeof(BaaMonsterDefinition).Assembly);
        var definition = registry.Monsters[new StringName("monster.baa")];
        var seen = new HashSet<StringName>();
        foreach (var seed in Enumerable.Range(1, 16))
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var session = new CreateMatchService(factory).Create((ulong)seed, 0, new PaladinHeroDefinition());
            var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
            var victory = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
                new BattleTick(1), 200, 0, Array.Empty<BattleEvent>());
            var wealth = session.Player.Wealth; var experience = session.Player.Experience;
            if (new MatchResultService(board).Apply(session, victory, MatchBattleKind.Monster, opponent: opponent).IsFailure
                || session.Player.Wealth != wealth + 3 || session.Player.Experience != experience + 2
                || session.PendingMonsterRewards.Count != 1) return false;
            var reward = session.PendingMonsterRewards.Single();
            if (!(reward.Kind == MonsterRewardKind.Card && (reward.Key == new StringName("card.silver_needle_grass") && reward.Level == 2
                    || reward.Key == new StringName("card.baa_sheep") && reward.Level == 1
                    || reward.Key == new StringName("card.windmill_daisy") && reward.Level == 1)
                || reward.Kind == MonsterRewardKind.Skill && reward.Key == new StringName("skill.healing") && reward.Level == 1)) return false;
            seen.Add(reward.Key);
            var claim = new MonsterRewardClaimService(registry, new CardEconomyService(factory, board),
                new SkillAcquisitionService(factory), board);
            if (claim.ClaimFirst(session).IsFailure || claim.ClaimFirst(session).IsSuccess || session.PendingMonsterRewards.Count != 0) return false;
            if (reward.Kind == MonsterRewardKind.Card)
            {
                if (session.Board.Battlefield.Count != 1 || session.Player.Inventory.Cards.Single().Attributes.Identity.Key != reward.Key
                    || session.Player.Inventory.Cards.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != reward.Level) return false;
            }
            else if (session.Player.Skills.Items.Single().Attributes.Identity.Key != reward.Key
                || session.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 1) return false;
        }
        return seen.Count == 4;
    }
}
