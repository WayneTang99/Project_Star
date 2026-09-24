using System;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Application.Economy;

public sealed record MonsterRewardClaimResult(PendingMonsterReward Reward, int CurrentLevel);

// 领取怪物战待领奖励，卡牌复用放置与合并、技能复用获得用例（应用层经济模块）。
public sealed class MonsterRewardClaimService
{
    private static readonly StringName NoReward = new("reward.none_pending");
    private static readonly StringName NoSpace = new("reward.board_full");
    private readonly DefinitionRegistry _registry;
    private readonly CardEconomyService _cards;
    private readonly SkillAcquisitionService _skills;
    private readonly BoardService _board;

    public MonsterRewardClaimService(
        DefinitionRegistry registry,
        CardEconomyService cards,
        SkillAcquisitionService skills,
        BoardService board)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _board = board ?? throw new ArgumentNullException(nameof(board));
    }

    // 只在领取成功后移除待领奖励；棋盘无法容纳卡牌时保留待领状态。
    public Result<MonsterRewardClaimResult> ClaimFirst(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.PendingMonsterRewards.Count == 0)
            return Result<MonsterRewardClaimResult>.Fail(new Failure(NoReward, "No monster reward is pending."));

        var reward = session.PendingMonsterRewards[0];
        int currentLevel;
        if (reward.Kind == MonsterRewardKind.Card)
        {
            var definition = _registry.Cards[reward.Key];
            var mergeTarget = _cards.FindMergeTarget(session, definition, reward.Level);
            var target = mergeTarget is null
                ? _board.FindFirstAvailableTarget(session, definition.Attributes.Identity.OccupiedSlots)
                : null;
            if (mergeTarget is null && target is null)
                return Result<MonsterRewardClaimResult>.Fail(new Failure(NoSpace, "Both boards are full."));

            var acquired = _cards.AcquireCard(session, definition, reward.Level, CardAcquisitionSource.Reward);
            if (acquired.IsFailure)
                return Result<MonsterRewardClaimResult>.Fail(acquired.Failure!);
            if (acquired.Value!.WasCreated)
            {
                var placed = _board.PlaceCard(session, acquired.Value.Card.Id, target!.Zone, target.Start);
                if (placed.IsFailure)
                {
                    session.Player.Inventory.Remove(acquired.Value.Card.Id);
                    return Result<MonsterRewardClaimResult>.Fail(placed.Failure!);
                }
            }
            currentLevel = acquired.Value.CurrentLevel;
        }
        else
        {
            var acquired = _skills.AcquireSkill(session, _registry.Skills[reward.Key], reward.Level);
            if (acquired.IsFailure)
                return Result<MonsterRewardClaimResult>.Fail(acquired.Failure!);
            currentLevel = acquired.Value!.CurrentLevel;
        }

        session.RemoveFirstPendingMonsterReward();
        return Result<MonsterRewardClaimResult>.Success(new MonsterRewardClaimResult(reward, currentLevel));
    }
}
