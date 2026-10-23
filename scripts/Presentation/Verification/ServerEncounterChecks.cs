using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Server;

namespace Project_Star.Presentation.Verification;

// 可达选项事件、探险转怪物及首轮路径的存档验证（表现层验证模块）。
internal static class ServerEncounterChecks
{
    internal static void Run(IDefinitionCatalog definitions, MatchSaveCodec codec, ServerMatchService service)
    {
        var run = FindEvent(service, "encounter.landfill");
        var initial = codec.Encode(run);
        Require(service.SelectEvent(run, new StringName("encounter.missing")).IsFailure && codec.Encode(run) == initial,
            "unissued event cannot advance");
        Require(service.SelectEvent(run, new StringName("encounter.landfill")).IsSuccess && run.Phase == ServerMatchPhase.Event
            && run.Event is { Round: 1, Turn: 1 } && run.Match.Progress.Turn == 2 && run.Match.Player.Experience == 1,
            "select event fixes issued visit and advances once");
        var opened = codec.Encode(run);
        run = codec.Decode(opened);
        var visitId = run.Event!.Id;
        Require(codec.Encode(run) == opened && !run.Event.Options.IsResolved && run.Event.Options.Options.Count == 2,
            "unresolved event options survive canonical restore");
        Require(service.LeaveEvent(run, visitId).IsFailure && service.SelectEvent(run, run.Event.EncounterKey).IsFailure
            && service.ResolveEvent(run, Guid.NewGuid(), run.Event.Options.Options[0].Key).IsFailure
            && service.ResolveEvent(run, visitId, new StringName("option.missing")).IsFailure && codec.Encode(run) == opened,
            "unresolved event rejects early exit redraw wrong visit and unissued option");
        var wealth = run.Match.Player.Wealth;
        Require(service.ResolveEvent(run, visitId, new StringName("encounter.landfill.wealth")).IsSuccess
            && run.Match.Player.Wealth == wealth + 2 && run.Event!.Result!.WealthGained == 2
            && run.Event.Options.IsResolved, "event wealth settles through formal option use case");
        var resolved = codec.Encode(run);
        run = codec.Decode(resolved);
        Require(codec.Encode(run) == resolved && service.ResolveEvent(run, visitId, new StringName("encounter.landfill.wealth")).IsFailure
            && codec.Encode(run) == resolved, "restored event result cannot grant twice");
        Require(service.LeaveEvent(run, visitId).IsSuccess && run.Event is null && run.Match.Progress.Turn == 2
            && run.Match.Player.Experience == 2 && run.Match.Player.Wealth == wealth + 2,
            "leave resolved event generates next choices with one turn experience");
        var left = codec.Encode(run);
        Require(service.LeaveEvent(run, visitId).IsFailure && codec.Encode(codec.Decode(left)) == left,
            "closed event cannot advance twice and choices restore");
        var corrupted = JsonNode.Parse(opened)!;
        corrupted["event"]!["options"]![0] = "encounter.landfill.material_small_card";
        Require(Rejects(() => codec.Decode(corrupted.ToJsonString())), "event option outside captured slot rejected");
        corrupted = JsonNode.Parse(opened)!;
        corrupted["event"]!["resolved"] = true;
        Require(Rejects(() => codec.Decode(corrupted.ToJsonString())), "event completion without result rejected");

        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), definitions.Sets);
        run = FindEvent(service, "encounter.training_ground");
        var hp = run.Match.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        _ = service.SelectEvent(run, new StringName("encounter.training_ground"));
        Require(service.ResolveEvent(run, run.Event!.Id, new StringName("encounter.training_ground.body_training")).IsSuccess
            && run.Match.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) == hp + 10
            && codec.Encode(codec.Decode(codec.Encode(run))) == codec.Encode(run), "hero training growth survives event restore");
        run = FindEvent(service, "encounter.training_ground");
        var attacker = AddCard(run, definitions, factory, board, "card.assassin_dagger", BoardZone.Battlefield, 0);
        var material = AddCard(run, definitions, factory, board, "card.silver_needle_grass", BoardZone.Battlefield, 1);
        var attack = attacker.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage);
        _ = service.SelectEvent(run, new StringName("encounter.training_ground"));
        Require(service.ResolveEvent(run, run.Event!.Id, new StringName("encounter.training_ground.sparring")).IsSuccess
            && attacker.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == attack + 5
            && !material.Attributes.BaseCombat.HasBaseValue(GameAttributeKeys.AttackDamage),
            "training changes eligible attackers without inventing material ability");
        run = FindEvent(service, "encounter.tavern");
        _ = service.SelectEvent(run, new StringName("encounter.tavern"));
        Require(service.ResolveEvent(run, run.Event!.Id, new StringName("encounter.tavern.cheers")).IsSuccess
            && run.Match.PendingBattleMaxHealthBonus == 10 && service.LeaveEvent(run, run.Event.Id).IsSuccess
            && codec.Decode(codec.Encode(run)).Match.PendingBattleMaxHealthBonus == 10,
            "tavern next battle health bonus persists across leaving");
        run = FindEvent(service, "encounter.tavern");
        _ = service.SelectEvent(run, new StringName("encounter.tavern"));
        run.Match.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 0);
        var poor = codec.Encode(run);
        Require(service.ResolveEvent(run, run.Event!.Id, new StringName("encounter.tavern.trade")).IsFailure
            && codec.Encode(run) == poor && !run.Event.Options.IsResolved, "event purchase failure retains unresolved visit and random");
        run.Match.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 2);
        Require(service.ResolveEvent(run, run.Event.Id, new StringName("encounter.tavern.trade")).IsSuccess
            && run.Match.Player.Wealth == 0 && run.Event.Result is { WealthSpent: 2, GrantedCardId: not null }
            && run.Match.Player.Inventory.Cards.Count == 1, "tavern trade uses formal purchase and placement");
        var granted = run.Match.Player.Inventory.Cards.Single();
        Require(service.SellCard(run, granted.Id).IsSuccess && run.Event.Result!.GrantedCardKey == granted.Attributes.Identity.Key
            && codec.Encode(codec.Decode(codec.Encode(run))) == codec.Encode(run), "event result retains reward identity after sale");

        run = FindEvent(service, "encounter.landfill");
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        for (var slot = 0; slot < 10; slot++)
            _ = AddCard(run, definitions, factory, board, "card.silver_needle_grass", zone, slot, 4);
        _ = service.SelectEvent(run, new StringName("encounter.landfill"));
        Require(service.ResolveEvent(run, run.Event!.Id, new StringName("encounter.landfill.material_small_card")).IsSuccess
            && run.Event.Result!.CardRewardSkipped && run.Match.Player.Inventory.Cards.Count == 20
            && run.Match.PendingMonsterRewards.Count == 0, "full board event reward follows existing skip rule without silent pending drop");

        // 后续轮次探险用明确的排程夹具，不冒充已完成前两轮PvP。
        var exploration = ExplorationFixture(service, 1, 2, 3);
        _ = service.SelectEvent(exploration, new StringName("encounter.quicksand_expanse"));
        var input = codec.Encode(exploration);
        var repeat = codec.Decode(input);
        Require(service.ResolveEvent(exploration, exploration.Event!.Id, new StringName("encounter.quicksand_expanse.pan_gold")).IsSuccess
            && service.ResolveEvent(repeat, repeat.Event!.Id, new StringName("encounter.quicksand_expanse.pan_gold")).IsSuccess
            && exploration.Event.Result is { WealthGained: >= 2 and <= 8 } && codec.Encode(exploration) == codec.Encode(repeat),
            "same saved exploration input repeats weighted random economy");
        ServerMatchSession? monsterRun = null;
        for (ulong seed = 1; seed <= 64 && monsterRun is null; seed++)
        {
            var candidate = ExplorationFixture(service, seed, 2, 3);
            _ = service.SelectEvent(candidate, new StringName("encounter.quicksand_expanse"));
            candidate = codec.Decode(codec.Encode(candidate));
            if (service.ResolveEvent(candidate, candidate.Event!.Id, new StringName("encounter.quicksand_expanse.cross_sandstorm")).IsSuccess
                && candidate.Phase == ServerMatchPhase.MonsterPreparation) monsterRun = candidate;
        }
        Require(monsterRun is { Battle: { Round: 3, Turn: 1, SourceEncounterKey: not null } }
            && monsterRun.Match.Progress.Turn == 2 && monsterRun.Event is null, "exploration monster enters original turn without second advance");
        var preparation = codec.Encode(monsterRun!);
        monsterRun = codec.Decode(preparation);
        Require(codec.Encode(monsterRun) == preparation && service.ResolveMonster(monsterRun, monsterRun.Battle!.Id).IsSuccess
            && monsterRun.Phase == ServerMatchPhase.MonsterResult, "event monster preparation restores and settles through formal battle");
        while (monsterRun.Match.PendingMonsterRewards.Count > 0)
            if (service.ClaimMonsterReward(monsterRun, monsterRun.Battle!.Id, 0).IsFailure) throw new InvalidOperationException("Event monster reward fixture failed.");
        Require(service.FinishMonster(monsterRun, monsterRun.Battle!.Id).IsSuccess && monsterRun.Match.Progress.Turn == 2,
            "event monster finishes into original next turn");
        var missingMonster = false;
        for (ulong seed = 1; seed <= 64 && !missingMonster; seed++)
        {
            var candidate = ExplorationFixture(service, seed, 5, 10);
            _ = service.SelectEvent(candidate, new StringName("encounter.quicksand_expanse"));
            missingMonster = service.ResolveEvent(candidate, candidate.Event!.Id, new StringName("encounter.quicksand_expanse.cross_sandstorm")).IsSuccess
                && candidate.Event?.Result is { MonsterEncounterSkipped: true } && codec.Encode(codec.Decode(codec.Encode(candidate))) == codec.Encode(candidate);
        }
        Require(missingMonster, "missing matching monster completes event with explicit skip result");
        CheckFirstRound(service, codec);
    }

    private static ServerMatchSession FindEvent(ServerMatchService service, string key)
    {
        for (ulong seed = 1; seed <= 128; seed++)
        {
            var run = service.Create(new StringName("hero.paladin"), seed).Value!;
            if (run.Match.EncounterSchedule.CurrentChoices.Any(choice => choice.Key == new StringName(key))) return run;
        }
        throw new InvalidOperationException("Event must be reachable from current formal first-round pool: " + key);
    }

    private static ServerMatchSession ExplorationFixture(ServerMatchService service, ulong seed, int level, int round)
    {
        var run = service.Create(new StringName("hero.paladin"), seed).Value!;
        run.Match.Progress.Round = round;
        run.Match.Progress.IncomeSettledThroughRound = round;
        run.Match.Progress.ExperienceSettledThroughTurn = (round - 1) * 8 + 1;
        run.Match.EncounterSchedule.Restore([], [new EncounterChoice(new StringName("encounter.quicksand_expanse"), "流沙地带", EncounterKind.Other) { Level = level }]);
        return run;
    }

    private static CardInstance AddCard(ServerMatchSession run, IDefinitionCatalog definitions, EntityFactory factory,
        BoardService board, string key, BoardZone zone, int start, int? level = null)
    {
        var card = factory.CreateCard(definitions.Cards[new StringName(key)], level);
        run.Match.Player.Inventory.Add(card);
        if (board.PlaceCard(run.Match, card.Id, zone, start).IsFailure) throw new InvalidOperationException("Event fixture placement failed.");
        return card;
    }

    private static void CheckFirstRound(ServerMatchService service, MatchSaveCodec codec)
    {
        var run = service.Create(new StringName("hero.paladin"), 1).Value!;
        var events = 0;
        while (run.Match.Progress.Turn < 8)
        {
            var choice = run.Match.EncounterSchedule.CurrentChoices.FirstOrDefault(value => value.Kind == EncounterKind.Other)
                ?? run.Match.EncounterSchedule.CurrentChoices.First();
            if (choice.Kind == EncounterKind.Monster)
            {
                if (service.SelectMonster(run, choice.Key).IsFailure || service.ResolveMonster(run, run.Battle!.Id).IsFailure)
                    throw new InvalidOperationException("First round monster failed.");
                while (run.Match.PendingMonsterRewards.Count > 0)
                    if (service.ClaimMonsterReward(run, run.Battle!.Id, 0).IsFailure) throw new InvalidOperationException("First round reward failed.");
                if (service.FinishMonster(run, run.Battle!.Id).IsFailure) throw new InvalidOperationException("First round monster finish failed.");
            }
            else if (choice.Kind == EncounterKind.Other)
            {
                if (service.SelectEvent(run, choice.Key).IsFailure) throw new InvalidOperationException("First round event failed.");
                run = codec.Decode(codec.Encode(run));
                var option = run.Event!.Options.Options.First(value => !value.Key.ToString().EndsWith(".trade", StringComparison.Ordinal));
                if (service.ResolveEvent(run, run.Event.Id, option.Key).IsFailure || service.LeaveEvent(run, run.Event.Id).IsFailure)
                    throw new InvalidOperationException("First round event resolution failed.");
                events++;
            }
            else if (service.SelectShop(run, choice.Key).IsFailure || service.LeaveShop(run).IsFailure)
                throw new InvalidOperationException("First round shop failed.");
            run = codec.Decode(codec.Encode(run));
        }
        Require(events > 0 && run.Match.Progress.Round == 1 && run.Match.Progress.Turn == 8
            && run.Match.EncounterSchedule.CurrentChoices.Count == 1 && run.Match.EncounterSchedule.CurrentChoices.Single().Kind == EncounterKind.Pvp,
            "formal events and monster path reaches strict turn eight PvP choice");
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("event verification: " + name);
        Console.WriteLine("SAVE_CHECK=" + name);
    }
}
