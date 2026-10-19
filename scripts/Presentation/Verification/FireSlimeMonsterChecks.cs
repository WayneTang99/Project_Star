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

// 火史莱姆怪物定义、遭遇、实战与奖励验证（表现层）。
internal static class FireSlimeMonsterChecks
{
    internal static bool DefinitionAndBattle()
    {
        var registry = DefinitionRegistry.Scan(typeof(FireSlimeMonsterDefinition).Assembly);
        var definition = registry.Monsters[new StringName("monster.fire_slime")];
        if (definition.Attributes.Identity.DisplayName != "火史莱姆" || definition.Level != 1
            || definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) != 40
            || !definition.Cards.SequenceEqual(new[]
            {
                new MonsterCardEntry(new StringName("card.lava_stone"), 1, 0),
                new MonsterCardEntry(new StringName("card.lava_stone"), 1, 1),
                new MonsterCardEntry(new StringName("card.fire_slime"), 1, 2),
            }) || !definition.Skills.SequenceEqual(new[] { new MonsterSkillEntry(new StringName("skill.burn_mastery"), 1) })) return false;
        using var image = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString()).GetImage();
        if (image.GetWidth() * 2 != image.GetHeight() * 3 || !image.HasMipmaps()) return false;
        GD.Print($"火史莱姆怪物原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        var seen = false;
        foreach (var seed in Enumerable.Range(1, 16))
        {
            var session = new CreateMatchService(new EntityFactory()).Create((ulong)seed, 0, new PaladinHeroDefinition());
            session.Progress.Turn = 4;
            var choice = new EncounterScheduler(registry).Generate(session).Value!
                .SingleOrDefault(item => item.Key == definition.Attributes.Identity.Key);
            if (choice is null) continue;
            if (choice.Kind != EncounterKind.Monster || choice.Level != 1
                || choice.Illustration != definition.Attributes.Identity.Illustration) return false;
            seen = true;
        }
        var player = new CreateMatchService(new EntityFactory()).Create(1, 0, new PaladinHeroDefinition());
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        if (opponent.Board.Battlefield.Placements.Count != 3
            || opponent.Player.Inventory.Cards.Count(card => card.Attributes.Identity.Key == new StringName("card.lava_stone")) != 2
            || opponent.Player.Inventory.Cards.Any(card => card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 1)
            || opponent.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 1) return false;
        var setup = new BattleSetupFactory().Create(player, opponent, 3, new BattleTick(31));
        var result = new CombatSimulator().Simulate(setup);
        var burns = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
        return seen && setup.Opponent.Hero.MaxHealth == 40 && setup.Opponent.Cards.Count == 3
            && burns.Length == 1 && burns[0].Amount == 4
            && result.Events.OfType<AbilityActivatedEvent>().Count() == 1
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
    }

    internal static bool Rewards()
    {
        var registry = DefinitionRegistry.Scan(typeof(FireSlimeMonsterDefinition).Assembly);
        var definition = registry.Monsters[new StringName("monster.fire_slime")];
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
            if (!(reward.Kind == MonsterRewardKind.Card && reward.Level == 1
                    && (reward.Key == new StringName("card.lava_stone") || reward.Key == new StringName("card.fire_slime"))
                || reward.Kind == MonsterRewardKind.Skill && reward.Key == new StringName("skill.burn_mastery") && reward.Level == 1)) return false;
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
        return seen.Count == 3;
    }
}
