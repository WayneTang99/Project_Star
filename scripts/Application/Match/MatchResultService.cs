using System;
using System.Collections.Generic;
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
    private readonly IEnumerable<CardDefinition>? _cards;

    public MatchResultService(BoardService boardService, IEnumerable<CardDefinition>? cards = null)
    {
        _boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));
        _quests = new CardQuestService(boardService);
        _cards = cards;
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
        foreach (var bonus in battle.PermanentAttributeBonuses)
        {
            if (bonus.Side != SideId.Player) continue;
            var card = session.Player.Inventory.Find(bonus.CardId);
            card?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), bonus.CardId, bonus.AttributeKey, bonus.Amount));
        }
        _quests.ApplyBattleProgress(session, battle);
        ApplyBattleBonuses(session, battle);
        if (battle.Outcome == BattleOutcome.PlayerVictory)
        {
            _quests.ProcessEvent(session, new BattleWonQuestEvent());
        }
        if (kind == MatchBattleKind.Monster)
        {
            SettleMonsterBattle(session, opponent!, battle);
            MatchAbilityService.ApplyVictory(session, battle, kind, _boardService, _cards);
            _boardService.RefreshBonuses(session);
            return Result.Success();
        }

        MatchAbilityService.ApplyVictory(session, battle, kind, _boardService, _cards);
        if (battle.Outcome == BattleOutcome.PlayerVictory) session.Progress.PvpWins++;
        else if (battle.Outcome == BattleOutcome.OpponentVictory)
            session.Player.LoseReputation(battleRound ?? session.Progress.Round);

        if (session.Progress.PvpWins >= 10) End(session, MatchStatus.Won, battleRound);
        else if (session.Player.Reputation <= 0) End(session, MatchStatus.Lost, battleRound);
        return Result.Success();
    }

    // 依据开战快照筛选实际参战卡牌，永久加成不读取本场临时数值。
    private static void ApplyBattleBonuses(MatchSession session, BattleResult battle)
    {
        if (battle.States.Count == 0) return;
        foreach (var participant in battle.States[0].Cards)
        {
            if (participant.Side != SideId.Player || participant.IsOnBench) continue;
            var card = session.Player.Inventory.Find(participant.Id);
            if (card is null) continue;
            if (card.BattleValueBonus > 0)
                card.Attributes.Persistent.ApplyModifier(new StatModifier(
                    ModifierId.New(), card.Id, GameAttributeKeys.Value, card.BattleValueBonus));
            if (battle.Outcome != BattleOutcome.PlayerVictory) continue;
            foreach (var bonus in card.BattleVictoryBonuses)
                card.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                    ModifierId.New(), card.Id, bonus.AttributeKey, bonus.Amount));
        }
    }

    private static void SettleMonsterBattle(MatchSession session, MatchSession opponent, BattleResult battle)
    {
        var monster = opponent.Player.Hero!;
        var maxHealth = monster.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var lostHealth = maxHealth - Math.Clamp(battle.OpponentRemainingHealth, 0, maxHealth);
        var level = monster.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level);
        session.Player.AddWealth(checked((int)(((long)level + 2) * lostHealth / maxHealth)));
        HeroExperienceService.Grant(session, checked((int)(((long)level + 1) * lostHealth / maxHealth)));
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

    // 放弃的是整局，区别于单场PvP认输；不结算奖励或推进随机。
    public Result AbandonMatch(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Status != MatchStatus.InProgress)
            return Result.Fail(new Failure(new Godot.StringName("match.already_ended"), "此对局已经结束。"));
        End(session, MatchStatus.Lost);
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

    private static void End(MatchSession session, MatchStatus status, int? completedRounds = null)
    {
        session.Status = status;
        session.Summary = new MatchSummary(
            status,
            completedRounds ?? session.Progress.Round,
            session.Progress.PvpWins,
            session.Player.Reputation,
            session.Player.Wealth);
    }
}
