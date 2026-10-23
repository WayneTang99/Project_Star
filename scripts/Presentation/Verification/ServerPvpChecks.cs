using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Server;

namespace Project_Star.Presentation.Verification;

// PvP冻结、一次结算及跨轮／终局规则验证；数值夹具不进入正式内容（表现层验证模块）。
internal static class ServerPvpChecks
{
    internal static void Run(IDefinitionCatalog definitions, MatchSaveCodec codec, ServerMatchService service)
    {
        var run = service.Create(new StringName("hero.paladin"), 19).Value!;
        var before = codec.Encode(run);
        Require(service.PreparePvp(run, Guid.NewGuid(), new StringName("missing"), 12, null, null).IsFailure
            && codec.Encode(run) == before, "PvP cannot start before turn eight");
        // 仅本规则夹具固定第8回合；正常到达路径已由ServerEncounterChecks另行验证。
        var player = Fixture(service, 1);
        var opponent = Fixture(service, 2);
        var key = player.Match.EncounterSchedule.CurrentChoices.Single().Key;
        var originalOpponent = codec.Encode(opponent);
        var originalPlayer = codec.Encode(player);
        Require(service.PreparePvp(player, Guid.NewGuid(), key, 3, codec.Decode(originalPlayer), Guid.NewGuid()).IsFailure
            && codec.Encode(player) == originalPlayer, "PvP rejects same match snapshot");
        var wrongRound = codec.Decode(originalOpponent);
        wrongRound.Match.Progress.Round++;
        Require(service.PreparePvp(player, Guid.NewGuid(), key, 3, wrongRound, Guid.NewGuid()).IsFailure
            && codec.Encode(player) == originalPlayer, "PvP rejects other round without advancing");
        Require(service.PreparePvp(player, Guid.NewGuid(), key, 3, codec.Decode(originalOpponent), null).IsFailure,
            "real PvP opponent requires snapshot identity");
        Boost(player, definitions);
        var wealth = player.Match.Player.Wealth;
        var income = player.Match.Player.Income;
        var reputation = player.Match.Player.Reputation;
        var battleId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        Require(service.PreparePvp(player, battleId, key, ulong.MaxValue - 7, codec.Decode(originalOpponent), snapshotId).IsSuccess
            && player.Phase == ServerMatchPhase.PvpPending && player.Match.Progress.Round == 2 && player.Match.Progress.Turn == 1
            && player.Match.Player.Wealth == wealth && player.Match.Progress.IncomeSettledThroughRound == 1,
            "PvP preparation fixes input before new round income");
        var frozen = codec.Encode(player);
        player = codec.Decode(frozen);
        Require(codec.Encode(player) == frozen && player.Pvp!.OpponentSnapshotId == snapshotId && !player.Pvp.IsTestOpponent,
            "pending PvP round trip preserves source and full opponent");
        var cardId = player.Match.Player.Inventory.Cards.Single().Id;
        Require(service.MoveCard(player, cardId, BoardZone.Bench, 0).IsFailure && service.SellCard(player, cardId).IsFailure
            && service.FinishPvp(player, battleId).IsFailure && service.PreparePvp(player, Guid.NewGuid(), key, 1, null, null).IsFailure
            && codec.Encode(player) == frozen, "pending PvP freezes board sale finish and rematching");
        Require(service.ResolvePvp(player, Guid.NewGuid()).IsFailure && codec.Encode(player) == frozen,
            "wrong PvP battle identity cannot resolve");
        Require(service.ResolvePvp(player, battleId).IsSuccess && player.Phase == ServerMatchPhase.PvpResult
            && player.Pvp!.Result!.Outcome == BattleOutcome.PlayerVictory && player.Match.Progress.PvpWins == 1
            && player.Match.Player.Reputation == reputation, "PvP victory increments one win without reputation loss");
        Require(codec.Encode(opponent) == originalOpponent, "resolving copied opponent leaves source match unchanged");
        var result = codec.Encode(player);
        player = codec.Decode(result);
        Require(codec.Encode(player) == result && service.ResolvePvp(player, battleId).IsFailure && codec.Encode(player) == result,
            "restored PvP result cannot settle twice");
        var repeat = codec.Decode(frozen);
        Require(service.ResolvePvp(repeat, battleId).IsSuccess && repeat.Pvp!.Result == player.Pvp!.Result
            && repeat.Match.Progress.PvpWins == player.Match.Progress.PvpWins, "same frozen PvP input repeats deterministic result");
        Require(service.FinishPvp(player, battleId).IsSuccess && player.Pvp is null && player.Phase == ServerMatchPhase.EncounterChoice
            && player.Match.Progress.Round == 2 && player.Match.Progress.Turn == 1
            && player.Match.Player.Wealth == wealth + income && player.Match.Progress.ExperienceSettledThroughTurn == 9,
            "PvP confirmation settles one new round income and experience");
        var next = codec.Encode(player);
        Require(service.FinishPvp(player, battleId).IsFailure && codec.Encode(codec.Decode(next)) == next,
            "PvP confirmation cannot advance or grant income twice");

        player = Fixture(service, 3, 3);
        opponent = Fixture(service, 4, 3);
        Boost(opponent, definitions);
        player.Match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1);
        battleId = Guid.NewGuid();
        Require(service.PreparePvp(player, battleId, key, 1, opponent, Guid.NewGuid()).IsSuccess
            && service.ResolvePvp(player, battleId).IsSuccess && player.Pvp!.Result!.Outcome == BattleOutcome.OpponentVictory
            && player.Match.Player.Reputation == 7 && player.Match.Progress.PvpWins == 0,
            "PvP defeat deducts captured battle round rather than next round");
        player = Fixture(service, 5);
        player.Match.Player.LoseReputation(9);
        player.Match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1);
        opponent = Fixture(service, 6);
        Boost(opponent, definitions);
        battleId = Guid.NewGuid();
        Require(service.PreparePvp(player, battleId, key, 1, opponent, Guid.NewGuid()).IsSuccess
            && service.ResolvePvp(player, battleId).IsSuccess && player.Match.Status == MatchStatus.Lost
            && player.Match.Summary is { CompletedRounds: 1, FinalReputation: 0 }
            && service.FinishPvp(player, battleId).IsFailure, "reputation zero ends at actual completed battle round");
        Require(codec.Encode(codec.Decode(codec.Encode(player))) == codec.Encode(player), "defeated terminal PvP result survives save restore");
        player = Fixture(service, 7);
        player.Match.Progress.PvpWins = 9;
        Boost(player, definitions);
        battleId = Guid.NewGuid();
        Require(service.PreparePvp(player, battleId, key, 1, Fixture(service, 8), Guid.NewGuid()).IsSuccess
            && service.ResolvePvp(player, battleId).IsSuccess && player.Match.Status == MatchStatus.Won
            && player.Match.Summary is { CompletedRounds: 1, PvpWins: 10 } && service.FinishPvp(player, battleId).IsFailure,
            "tenth PvP win ends without opening another round");
        Require(codec.Encode(codec.Decode(codec.Encode(player))) == codec.Encode(player), "victorious terminal PvP result survives save restore");
        player = Fixture(service, 9);
        battleId = Guid.NewGuid();
        Require(service.PreparePvp(player, battleId, key, 1, Fixture(service, 10), Guid.NewGuid()).IsSuccess
            && service.ResolvePvp(player, battleId).IsSuccess && player.Pvp!.Result!.Outcome == BattleOutcome.PlayerVictory
            && player.Pvp.Result.EndReason == BattleEndReason.SimultaneousDefeat
            && player.Match.Progress.PvpWins == 1 && player.Match.Player.Reputation == 10,
            "PvP simultaneous defeat follows existing challenger victory rule");
        player = Fixture(service, 11, 3);
        Require(service.PreparePvp(player, Guid.NewGuid(), key, 1, null, null).IsSuccess
            && player.Pvp is { IsTestOpponent: true, OpponentSnapshotId: null, Round: 3 }
            && player.Pvp.Opponent.Progress.Round == 3 && player.Pvp.Opponent.Progress.Turn == 8
            && codec.Encode(codec.Decode(codec.Encode(player))) == codec.Encode(player),
            "empty pool fixture is fixed marked and matches round");
        var corrupt = JsonNode.Parse(frozen)!;
        corrupt["pvp"]!["isTestOpponent"] = true;
        Require(Rejects(() => codec.Decode(corrupt.ToJsonString())), "PvP source marker mismatch rejected");
    }

    private static ServerMatchSession Fixture(ServerMatchService service, ulong seed, int round = 1)
    {
        var run = service.Create(new StringName("hero.paladin"), seed).Value!;
        run.Match.Progress.Round = round;
        run.Match.Progress.Turn = 8;
        run.Match.Progress.IncomeSettledThroughRound = round;
        run.Match.Progress.ExperienceSettledThroughTurn = round * 8;
        run.Match.EncounterSchedule.Restore([], [new EncounterChoice(new StringName("encounter.pvp"), "PvP验证夹具", EncounterKind.Pvp)]);
        return run;
    }

    private static void Boost(ServerMatchSession run, IDefinitionCatalog definitions)
    {
        var card = new EntityFactory().CreateCard(definitions.Cards[new StringName("card.assassin_dagger")]);
        card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.AttackDamage, 10000);
        card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.CooldownTicks, 10);
        run.Match.Player.Inventory.Add(card);
        run.Match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        if (new BoardService(new BoardPlacementSolver(), definitions.Sets).PlaceCard(run.Match, card.Id, BoardZone.Battlefield, 0).IsFailure)
            throw new InvalidOperationException("PvP boost fixture placement failed.");
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("PvP verification: " + name);
        Console.WriteLine("SAVE_CHECK=" + name);
    }
}
