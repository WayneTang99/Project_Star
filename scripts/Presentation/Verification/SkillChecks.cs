using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Content.Heroes;
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

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
    internal static bool CheckDefendSkill()
    {
        var definition = new DefendSkillDefinition();
        var factory = new EntityFactory();
        var multipliers = new[] { 10, 20, 30, 40 };
        var heroLevels = new[] { 1, 3, 5, 2 };
        for (var level = 1; level <= 4; level++)
        {
            var skill = factory.CreateSkill(definition, level);
            var setup = new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 7, Level: heroLevels[level - 1]),
                    [], [new SkillBattleSetup(skill.Id, skill.Abilities,
                        skill.Attributes.BaseCombat.SnapshotFinalValues())]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 11, Level: 9),
                    [new CardBattleSetup(EntityId.New(), 0, 3, 1)]),
                1, new BattleTick(4), new BattleTick(300));
            var result = new CombatSimulator().Simulate(setup);
            var initialArmor = 7 + heroLevels[level - 1] * multipliers[level - 1];
            var activations = result.Events.OfType<AbilityActivatedEvent>()
                .Where(value => value.SourceCardId == skill.Id).ToArray();
            var damage = result.Events.OfType<DamageDealtEvent>()
                .Where(value => value.TargetSide == SideId.Player).ToArray();
            var details = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, level));
            if (activations.Length != 1 || activations[0].Tick != new BattleTick(0)
                || activations[0].SourceKind != AbilitySourceKind.Skill || activations[0].IsEcho
                || result.States[0].Player.Armor != 7
                || result.States.Last(value => value.Tick == new BattleTick(0)).Player.Armor != initialArmor
                || damage.Length == 0 || damage.Any(value => value.Tick == new BattleTick(0) || value.HealthDamage != 0)
                || result.States.Last().Player.Armor != initialArmor - damage.Sum(value => value.ArmorAbsorbed)
                || result.States.Any(value => value.Opponent.Armor != 11)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || !details.Contains("战斗开始") || !details.Contains($"己方英雄等级 × {multipliers[level - 1]}"))
                return false;
        }
        try
        {
            _ = new AbilityDefinition("ability.verification.invalid_armor", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroLevelScaledArmorEffectDefinition(-1)]);
            return false;
        }
        catch (ArgumentException) { }
        return definition.InitialLevel == 1 && definition.Attributes.Identity.FactionKey == new StringName("paladin")
            && !definition.SupportsLevel(5);
    }

    internal static bool CheckDefendMentorAndIsolation()
    {
        var registry = DefinitionRegistry.Scan(typeof(SkillChecks).Assembly);
        if (!registry.Skills.TryGetValue("skill.defend", out var definition)
            || definition is not DefendSkillDefinition) return false;
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var player = matches.Create(1, 0, new PaladinHeroDefinition());
        var opponent = matches.Create(2, 0, new PaladinHeroDefinition());
        var acquisition = new SkillAcquisitionService(factory);
        var mentors = new MentorService(registry, acquisition);
        var visit = mentors.Open(player, "mentor.archbishop").Value!;
        if (!visit.Offers.Any(value => value.SkillKey == definition.Attributes.Identity.Key && value.Level == 1)
            || mentors.ChooseSkill(player, visit, definition.Attributes.Identity.Key).IsFailure) return false;
        var second = mentors.Open(player, "mentor.archbishop").Value!;
        var merged = mentors.ChooseSkill(player, second, definition.Attributes.Identity.Key);
        if (merged.IsFailure || merged.Value!.CurrentLevel != 2 || player.Player.Skills.Items.Count != 1)
            return false;
        _ = acquisition.AcquireSkill(player, definition, 3);
        _ = acquisition.AcquireSkill(opponent, definition, 1);
        player.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 3);
        player.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 7);
        opponent.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 4);
        opponent.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 9);
        var setups = new BattleSetupFactory();
        var setup = setups.Create(player, opponent, 3, new BattleTick(1), new BattleTick(300));
        player.Player.Hero.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 5);
        var result = new CombatSimulator().Simulate(setup);
        var next = new CombatSimulator().Simulate(
            setups.Create(player, opponent, 3, new BattleTick(1), new BattleTick(300)));
        return setup.Player.Hero.Level == 3 && setup.Player.Hero.Armor == 7
            && result.States.Last().Player.Armor == 157 && result.States.Last().Opponent.Armor == 49
            && next.States.Last().Player.Armor == 257 && next.States.Last().Opponent.Armor == 49
            && result.PermanentChanges.Count == 0 && next.PermanentChanges.Count == 0
            && player.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 7
            && opponent.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 9;
    }

}
