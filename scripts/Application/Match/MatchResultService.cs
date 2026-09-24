using System;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Match;

public enum MatchBattleKind
{
    Monster = 0,
    Pvp = 1,
}

/// <summary>Applies only persistent battle consequences to the match aggregate.</summary>
public sealed class MatchResultService
{
    private readonly BoardService _boardService;
    private readonly CardQuestService _quests;

    public MatchResultService(BoardService boardService)
    {
        _boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));
        _quests = new CardQuestService(boardService);
    }

    public Result Apply(
        MatchSession session,
        BattleResult battle,
        MatchBattleKind kind,
        int? battleRound = null,
        MatchSession? opponent = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);
        if (session.Status != MatchStatus.InProgress)
            return Result.Fail(new Failure(new Godot.StringName("match.already_ended"), "The match has already ended."));
        if (kind == MatchBattleKind.Monster
            && (opponent?.Player.Hero is not { } monster
                || monster.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) < 1
                || monster.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level) < 1))
            return Result.Fail(new Failure(new Godot.StringName("match.invalid_monster"), "A valid monster opponent is required."));

        ApplyPermanentChanges(session, battle);
        if (battle.Outcome == BattleOutcome.PlayerVictory)
            _quests.ProcessEvent(session, new BattleWonQuestEvent());
        if (kind == MatchBattleKind.Monster)
        {
            SettleMonsterBattle(session, opponent!, battle);
            return Result.Success();
        }

        if (battle.Outcome == BattleOutcome.PlayerVictory) session.Progress.PvpWins++;
        else if (battle.Outcome == BattleOutcome.OpponentVictory)
            session.Player.LoseReputation(battleRound ?? session.Progress.Round);

        if (session.Progress.PvpWins >= 10) End(session, MatchStatus.Won);
        else if (session.Player.Reputation <= 0) End(session, MatchStatus.Lost);
        return Result.Success();
    }

    private static void SettleMonsterBattle(MatchSession session, MatchSession opponent, BattleResult battle)
    {
        var monster = opponent.Player.Hero!;
        var maxHealth = monster.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var lostHealth = maxHealth - Math.Clamp(battle.OpponentRemainingHealth, 0, maxHealth);
        var level = monster.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level);
        session.Player.AddWealth(checked((int)(((long)level + 2) * lostHealth / maxHealth)));
        session.Player.AddExperience(checked((int)(((long)level + 1) * lostHealth / maxHealth)));
        if (battle.Outcome != BattleOutcome.PlayerVictory) return;

        var candidates = new System.Collections.Generic.List<PendingMonsterReward>();
        foreach (var card in opponent.Player.Inventory.Cards)
            candidates.Add(new PendingMonsterReward(
                MonsterRewardKind.Card,
                card.Attributes.Identity.Key,
                card.Attributes.Identity.DisplayName,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level)));
        foreach (var skill in opponent.Player.Skills.Items)
            candidates.Add(new PendingMonsterReward(
                MonsterRewardKind.Skill,
                skill.Attributes.Identity.Key,
                skill.Attributes.Identity.DisplayName,
                skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level)));
        if (candidates.Count == 0) return;
        var random = new SeededRandom(session.Random.State);
        session.AddPendingMonsterReward(candidates[random.NextInt(0, candidates.Count)]);
        session.Random.State = random.State;
    }

    public Result Surrender(MatchSession session, MatchBattleKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (kind == MatchBattleKind.Pvp)
        {
            session.Player.LoseReputation(session.Progress.Round);
            if (session.Player.Reputation <= 0) End(session, MatchStatus.Lost);
        }
        return Result.Success();
    }

    private void ApplyPermanentChanges(MatchSession session, BattleResult battle)
    {
        foreach (var change in battle.PermanentChanges)
        {
            if (change.ChangeType != "Destroy") continue;
            if (session.Board.Contains(change.CardId)) _ = _boardService.RemoveFromBoard(session, change.CardId);
            session.Player.Inventory.Remove(change.CardId);
        }
    }

    private static void End(MatchSession session, MatchStatus status)
    {
        session.Status = status;
        session.Summary = new MatchSummary(
            status,
            session.Progress.Round,
            session.Progress.PvpWins,
            session.Player.Reputation,
            session.Player.Wealth);
    }
}
