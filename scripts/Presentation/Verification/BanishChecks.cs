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
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 放逐的分级随机禁锢、冷却、过滤与技能冻结验证（表现层）。
internal static class BanishChecks
{
    internal static bool LevelsAndTargets()
    {
        var definition = new BanishSkillDefinition();
        var registry = DefinitionRegistry.Scan(typeof(BanishSkillDefinition).Assembly);
        if (!registry.Skills.ContainsKey(new StringName("skill.banish")) || definition.InitialLevel != 2
            || definition.Attributes.Identity.DisplayName != "放逐" || definition.Attributes.Identity.FactionKey != GameFactions.Neutral
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)) return false;
        foreach (var level in Enumerable.Range(2, 3))
        foreach (var enemySource in new[] { false, true })
        {
            var duration = (level - 1) * 10;
            var skill = Skill(level);
            var ability = skill.Abilities.Single();
            var details = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, level));
            if (ability.Activation != AbilityActivation.PassiveOnBattleStart || ability.ManaCost != 0 || ability.CooldownTicks != 0
                || ability.Effects.Count != 2
                || ability.Effects[0] is not ApplyStatusToRandomAlliedCardEffectDefinition { Status: BattleStatus.ImmobilizeDuration, Amount: var allyAmount }
                || allyAmount != duration
                || ability.Effects[1] is not ApplyStatusToRandomEnemyCardEffectDefinition { Status: BattleStatus.ImmobilizeDuration, Amount: var enemyAmount, TargetCount: 1 }
                || enemyAmount != duration || !details.Contains("双方战场卡牌") || !details.Contains($"禁锢 {level - 1}秒")
                || details.Contains("ImmobilizeDuration")) return false;
            var playerCards = Enumerable.Range(0, 4).Select(index => Card(index, index == 3)).ToArray();
            var enemyCards = Enumerable.Range(0, 4).Select(index => Card(index, index == 3)).ToArray();
            var selectedTargets = new HashSet<EntityId>();
            foreach (var seed in Enumerable.Range(1, 8))
            {
                var setup = Setup(playerCards, enemyCards, enemySource ? [] : [skill], enemySource ? [skill] : [],
                    (ulong)seed, duration + 11);
                var battle = new CombatSimulator().Simulate(setup);
                var opening = Opening(battle);
                var activations = battle.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == skill.EntityId).ToArray();
                if (activations.Length != 1 || activations[0].Tick.Value != 0 || activations[0].IsEcho
                    || activations[0].SourceKind != AbilitySourceKind.Skill
                    || battle.Events.OfType<StatusChangedEvent>().Count(item => item.Tick.Value == 0
                        && item.Status == BattleStatus.ImmobilizeDuration && item.Amount == duration) != 2
                    || !battle.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    || opening.Cards.Where(card => card.IsOnBench).Any(card => card.Immobilize != 0)) return false;
                foreach (var side in new[] { SideId.Player, SideId.Opponent })
                {
                    var target = opening.Cards.Where(card => card.Side == side && card.Immobilize == duration).ToArray();
                    if (target.Length != 1) return false;
                    selectedTargets.Add(target[0].Id);
                }
                foreach (var card in opening.Cards.Where(card => !card.IsOnBench))
                {
                    var first = battle.Events.OfType<AbilityActivatedEvent>().First(item => item.SourceCardId == card.Id);
                    if (first.Tick.Value != 10 + card.Immobilize) return false;
                }
                if (battle.States.Last(frame => frame.Tick.Value == duration).Cards.Any(card => card.Immobilize != 0)) return false;
            }
            if (selectedTargets.Count <= 2) return false;
        }
        return true;
    }

    internal static bool StackingAndFilters()
    {
        var player = Card(0); var enemy = Card(0);
        var stacked = new CombatSimulator().Simulate(Setup([player], [enemy], [Skill(2)], [Skill(3)], 1, 41));
        if (Opening(stacked).Cards.Any(card => card.Immobilize != 30)
            || stacked.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceKind == AbilitySourceKind.Skill).Count() != 2
            || stacked.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceKind == AbilitySourceKind.Card)
                .Any(item => item.Tick.Value != 40)) return false;
        var flying = new AbilityDefinition(new StringName("verification.opening_flight"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)]);
        player = player with { Abilities = player.Abilities!.Concat(new[] { flying }).ToArray() };
        enemy = enemy with { Abilities = enemy.Abilities!.Concat(new[] { flying }).ToArray() };
        var flightResult = new CombatSimulator().Simulate(Setup([player], [enemy], [], [Skill(2)], 1, 16));
        if (Opening(flightResult).Cards.Any(card => !card.IsFlying || card.Immobilize != 5)
            || flightResult.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceKind == AbilitySourceKind.Card
                && item.Tick.Value > 0).Any(item => item.Tick.Value != 15)) return false;
        var destroy = new AbilityDefinition(new StringName("verification.opening_destroy"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
        var destroyed = Card(1) with { Abilities = [destroy] };
        var bench = Card(2, true); var live = Card(0);
        var filtered = new CombatSimulator().Simulate(Setup([live, destroyed, bench], [Card(0)], [], [Skill(2)], 2, 1));
        var state = Opening(filtered);
        if (state.Cards.Single(card => card.Id == live.EntityId).Immobilize != 10
            || !state.Cards.Single(card => card.Id == destroyed.EntityId).Destroyed
            || state.Cards.Single(card => card.Id == destroyed.EntityId).Immobilize != 0
            || state.Cards.Single(card => card.Id == bench.EntityId).Immobilize != 0) return false;
        foreach (var emptyPlayer in new[] { false, true })
        {
            var result = new CombatSimulator().Simulate(Setup(emptyPlayer ? [] : [Card(0)], emptyPlayer ? [Card(0)] : [],
                [Skill(2)], [], 1, 1));
            if (Opening(result).Cards.Single().Immobilize != 10
                || result.Events.OfType<StatusChangedEvent>().Count() != 1) return false;
        }
        var empty = new CombatSimulator().Simulate(Setup([], [], [Skill(2)], [], 1, 1));
        return !empty.Events.OfType<StatusChangedEvent>().Any()
            && empty.Events.OfType<AbilityActivatedEvent>().Count() == 1;
    }

    internal static bool MergeAndSnapshots()
    {
        var definition = new BanishSkillDefinition();
        var factory = new EntityFactory(); var matches = new CreateMatchService(factory);
        var session = matches.Create(1, 0, new PaladinHeroDefinition());
        var opponent = matches.Create(2, 0, new PaladinHeroDefinition());
        var board = new BoardService(new BoardPlacementSolver());
        foreach (var owner in new[] { session, opponent })
        {
            var card = factory.CreateCard(new FlangedMaceCardDefinition()); owner.Player.Inventory.Add(card);
            if (board.PlaceCard(owner, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        }
        var service = new SkillAcquisitionService(factory);
        var first = service.AcquireSkill(session, definition, 2).Value!;
        var oldSnapshot = MatchSnapshot.From(session).Skills.Single();
        var oldSetup = new BattleSetupFactory().Create(session, opponent, 3, new BattleTick(1));
        var random = session.Random.State;
        var merged = service.AcquireSkill(session, definition, 2).Value!;
        var newSetup = new BattleSetupFactory().Create(session, opponent, 3, new BattleTick(1));
        if (merged.WasCreated || merged.CurrentLevel != 3 || merged.Skill.Id != first.Skill.Id
            || session.Random.State != random || session.Board.Battlefield.Count != 1
            || oldSnapshot.Level != 2 || MatchSnapshot.From(session).Skills.Single().Level != 3
            || !CardDisplayAdapter.SkillDetails(oldSnapshot).Contains("禁锢 1秒")
            || Opening(new CombatSimulator().Simulate(oldSetup)).Cards.Any(card => card.Immobilize != 10)
            || Opening(new CombatSimulator().Simulate(newSetup)).Cards.Any(card => card.Immobilize != 20)) return false;
        var four = service.AcquireSkill(session, definition, 3).Value!;
        return four.CurrentLevel == 4 && four.Skill.Id == first.Skill.Id
            && ((ApplyStatusToRandomAlliedCardEffectDefinition)four.Skill.Abilities.Single().Effects[0]).Amount == 30;
    }

    private static SkillBattleSetup Skill(int level)
    {
        var skill = new EntityFactory().CreateSkill(new BanishSkillDefinition(), level);
        return new SkillBattleSetup(skill.Id, skill.Abilities, skill.Attributes.BaseCombat.SnapshotFinalValues());
    }

    private static CardBattleSetup Card(int start, bool bench = false) => new(EntityId.New(), start, 1, 10,
        [new AbilityDefinition(new StringName("verification.attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 10, [new DamageEffectDefinition(1)])], IsOnBench: bench, UseLegacyAttack: false);

    private static BattleSetup Setup(CardBattleSetup[] player, CardBattleSetup[] opponent,
        SkillBattleSetup[] playerSkills, SkillBattleSetup[] opponentSkills, ulong seed, int timeout) => new(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), player, playerSkills),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), opponent, opponentSkills),
            seed, new BattleTick(timeout), new BattleTick(300));

    private static BattleStateSnapshot Opening(BattleResult battle) => battle.States.Last(frame => frame.Tick.Value == 0);
}
