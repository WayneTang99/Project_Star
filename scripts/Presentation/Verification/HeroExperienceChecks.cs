using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 回合经验幂等、升级阈值、奖励经验及重开隔离验证（表现层验证模块）。
internal static class HeroExperienceChecks
{
    internal static bool TurnsAndThresholds()
    {
        var factory = new EntityFactory(); var create = new CreateMatchService(factory);
        var match = create.Create(42, 100, new MonaHeroDefinition());
        var service = new TurnExperienceService();
        var scheduler = new EncounterScheduler(DefinitionRegistry.Scan(typeof(MonaHeroDefinition).Assembly), true);
        if (match.Player.Experience != 1 || service.SettleCurrentTurn(match).Value != 0) return false;
        for (var index = 2; index <= 10; index++)
        {
            if (scheduler.Generate(match).IsFailure || scheduler.Select(match, match.EncounterSchedule.CurrentChoices[0].Key).IsFailure
                || scheduler.Generate(match).IsFailure) return false;
            scheduler.Generate(match);
            if (match.Player.Experience != index % 10 || service.SettleCurrentTurn(match).Value != 0) return false;
        }
        if (match.Progress.Round != 2 || match.Progress.Turn != 2
            || match.Player.Hero!.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || match.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != 140
            || match.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana) != 140) return false;
        match.Player.AddExperience(25);
        var snapshot = MatchSnapshot.From(match);
        if (snapshot.Experience != 5 || snapshot.Hero!.Level != 4
            || snapshot.Hero.CombatValues[GameAttributeKeys.MaxHealth] != 280
            || snapshot.Hero.CombatValues[GameAttributeKeys.MaxMana] != 280) return false;
        var opponent = create.Create(43, 100, new MonaHeroDefinition());
        opponent.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 4);
        var result = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
            new BattleTick(1), 100, 0, System.Array.Empty<BattleEvent>());
        new MatchResultService(new BoardService(new BoardPlacementSolver())).Apply(match, result, MatchBattleKind.Monster, opponent: opponent);
        if (match.Player.Experience != 0 || match.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 5
            || match.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != 380
            || match.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana) != 380) return false;
        var frozen = new BattleSetupFactory().Create(match, opponent, 42, new BattleTick(1));
        match.Player.Hero.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), match.Player.Hero.Id, GameAttributeKeys.MaxHealth, 17));
        match.Player.Hero.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), match.Player.Hero.Id, GameAttributeKeys.MaxMana, 11));
        match.Player.AddExperience(10);
        var next = new BattleSetupFactory().Create(match, opponent, 42, new BattleTick(1));
        if (next.Player.Hero.MaxHealth != 517 || next.Player.Hero.MaxMana != 511
            || frozen.Player.Hero.MaxHealth != 380 || frozen.Player.Hero.MaxMana != 380
            || snapshot.Hero.CombatValues[GameAttributeKeys.MaxHealth] != 280) return false;
        var fresh = create.Create(42, 100, new MonaHeroDefinition());
        if (fresh.Player.Experience != 1 || fresh.Player.Hero!.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 1
            || fresh.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != 100
            || fresh.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana) != 100) return false;
        match.Status = MatchStatus.Won; match.Progress.Turn++;
        if (service.SettleCurrentTurn(match).Value != 0 || match.Player.Experience != 0) return false;
        var missingHero = new MatchSession(42);
        return service.SettleCurrentTurn(missingHero).IsFailure && missingHero.Progress.ExperienceSettledThroughTurn == 0;
    }
}
