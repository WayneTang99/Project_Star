using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 独立技能的合并与触发验证（表现层验证模块）。
internal static class SkillChecks
{
    internal static bool CheckSkillMerge()
    {
        var definition = new VerificationSkillDefinition();
        var session = new MatchSession(1);
        var service = new SkillAcquisitionService(new EntityFactory());
        var first = service.AcquireSkill(session, definition, 1).Value!;
        var id = first.Skill.Id;
        var levelTwo = service.AcquireSkill(session, definition, 1).Value!;
        var levelThree = service.AcquireSkill(session, definition, 2).Value!;
        var levelFour = service.AcquireSkill(session, definition, 3).Value!;
        var extraFour = service.AcquireSkill(session, definition, 4).Value!;
        var directFive = service.AcquireSkill(session, definition, 5).Value!;
        var snapshot = MatchSnapshot.From(session);
        var chainSession = new MatchSession(2);
        var chainThree = service.AcquireSkill(chainSession, definition, 3).Value!;
        _ = service.AcquireSkill(chainSession, definition, 2);
        _ = service.AcquireSkill(chainSession, definition, 1);
        var chain = service.AcquireSkill(chainSession, definition, 1).Value!;
        return first.WasCreated
            && !levelTwo.WasCreated && !levelThree.WasCreated && !levelFour.WasCreated
            && levelFour.Skill.Id == id && levelFour.CurrentLevel == 4
            && extraFour.WasCreated && directFive.WasCreated
            && session.Player.Skills.Items.Count == 3
            && snapshot.Skills.Count == 3
            && levelFour.Skill.Abilities[0].Effects[0] is DamageEffectDefinition { Amount: 20 }
            && chain.CurrentLevel == 4 && chainSession.Player.Skills.Items.Count == 1
            && chainSession.Player.Skills.Items[0].Id == chain.Skill.Id
            && chainSession.Player.Skills.Items[0].Id != chainThree.Skill.Id;
    }

    internal static bool CheckSkillBattle()
    {
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var hero = new VerificationHeroDefinition();
        var player = matches.Create(1, 0, hero);
        var opponent = matches.Create(2, 0, hero);
        var skill = new SkillAcquisitionService(factory)
            .AcquireSkill(player, new VerificationSkillDefinition(), 1).Value!.Skill;
        var attacker = factory.CreateCard(new VerificationCardDefinition());
        opponent.Player.Inventory.Add(attacker);
        _ = CreateBoardService().PlaceCard(opponent, attacker.Id, BoardZone.Battlefield, 0);
        var setup = new BattleSetupFactory().Create(player, opponent, 99, new BattleTick(12), new BattleTick(300));
        var result = new CombatSimulator().Simulate(setup);
        return player.Board.Battlefield.Count == 0
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
            && result.Events.OfType<AbilityActivatedEvent>().Any(value =>
                value.SourceCardId == skill.Id && value.SourceKind == AbilitySourceKind.Skill && value.IsEcho)
            && result.Events.OfType<DamageDealtEvent>().Any(value =>
                value.SourceCardId == skill.Id && value.SourceKind == DamageSourceKind.Skill
                && value.TargetSide == SideId.Opponent && value.RawDamage == 5)
            && result.Events.OfType<AbilityActivatedEvent>().Count(value => value.SourceCardId == skill.Id) == 1;
    }

    internal static bool CheckChargeSkill()
    {
        var definition = new ChargeSkillDefinition();
        var factory = new EntityFactory();
        var multipliers = new[] { 10, 20, 30, 40 };
        var heroLevels = new[] { 1, 3, 5, 2 };
        for (var level = 1; level <= 4; level++)
        {
            var skill = factory.CreateSkill(definition, level);
            var playerCard = new CardBattleSetup(EntityId.New(), 0, 0, 2);
            var opponentCard = new CardBattleSetup(EntityId.New(), 0, 1, 1);
            var setup = new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0, Level: heroLevels[level - 1]),
                    [playerCard], [new SkillBattleSetup(skill.Id, skill.Abilities,
                        skill.Attributes.BaseCombat.SnapshotFinalValues())]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 5), [opponentCard]),
                1, new BattleTick(5), new BattleTick(300));
            var events = new CombatSimulator().Simulate(setup).Events;
            var charge = events.OfType<DamageDealtEvent>()
                .Where(value => value.SourceCardId == skill.Id).ToArray();
            if (charge.Length != 1
                || charge[0].SourceKind != DamageSourceKind.Skill
                || charge[0].TargetSide != SideId.Opponent
                || charge[0].RawDamage != heroLevels[level - 1] * multipliers[level - 1]
                || charge[0].ArmorAbsorbed != 5
                || charge[0].Tick != new BattleTick(2)
                || skill.Abilities[0].Effects[0] is not SourceHeroLevelScaledDamageEffectDefinition levelEffect
                || levelEffect.Multiplier != multipliers[level - 1]
                || events.OfType<AbilityActivatedEvent>().Count(value =>
                    value.SourceCardId == skill.Id && value.IsEcho) != 1
                || events.OfType<AbilityActivatedEvent>().Count(value =>
                    value.SourceCardId == playerCard.EntityId) < 2
                || !events.OfType<AbilityActivatedEvent>().Any(value =>
                    value.SourceCardId == opponentCard.EntityId && value.Tick == new BattleTick(1)))
                return false;
        }

        var idleSkill = factory.CreateSkill(definition);
        var idleSetup = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0), [],
                [new SkillBattleSetup(idleSkill.Id, idleSkill.Abilities,
                    idleSkill.Attributes.BaseCombat.SnapshotFinalValues())]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0),
                [new CardBattleSetup(EntityId.New(), 0, 1, 1)]),
            2, new BattleTick(2), new BattleTick(300));
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        player.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 7);
        return !new CombatSimulator().Simulate(idleSetup).Events.OfType<AbilityActivatedEvent>()
                .Any(value => value.SourceCardId == idleSkill.Id)
            && new BattleSetupFactory().Create(player, opponent, 1, new BattleTick(1)).Player.Hero.Level == 7;
    }


}
