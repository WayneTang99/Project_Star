using System;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Economy;

public sealed record MonsterRewardClaimResult(PendingMonsterReward Reward, int CurrentLevel);

// 领取怪物战待领奖励，卡牌复用放置与合并、技能复用获得用例（应用层经济模块）。
public sealed class MonsterRewardClaimService
{
    private static readonly StringName NoReward = new("reward.none_pending");
    private readonly IDefinitionCatalog _registry;
    private readonly CardEconomyService _cards;
    private readonly SkillAcquisitionService _skills;

    public MonsterRewardClaimService(
        IDefinitionCatalog registry,
        CardEconomyService cards,
        SkillAcquisitionService skills,
        BoardService board)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        ArgumentNullException.ThrowIfNull(board);
    }

    // 只在领取成功后移除待领奖励；棋盘无法容纳卡牌时保留待领状态。
    public Result<MonsterRewardClaimResult> ClaimFirst(MatchSession session)
        => Claim(session, 0);

    // 按当前待领奖励索引领取；失败保留原列表，卡牌走完整获得事务。
    public Result<MonsterRewardClaimResult> Claim(MatchSession session, int index)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (index < 0 || index >= session.PendingMonsterRewards.Count)
            return Result<MonsterRewardClaimResult>.Fail(new Failure(NoReward, "No monster reward is pending."));

        var reward = session.PendingMonsterRewards[index];
        int currentLevel;
        if (reward.Kind == MonsterRewardKind.Card)
        {
            var definition = _registry.Cards[reward.Key];
            var acquired = _cards.AcquireAndPlace(session, definition, reward.Level, CardAcquisitionSource.Reward);
            if (acquired.IsFailure)
                return Result<MonsterRewardClaimResult>.Fail(acquired.Failure!);
            currentLevel = acquired.Value!.CurrentLevel;
        }
        else
        {
            var acquired = _skills.AcquireSkill(session, _registry.Skills[reward.Key], reward.Level);
            if (acquired.IsFailure)
                return Result<MonsterRewardClaimResult>.Fail(acquired.Failure!);
            currentLevel = acquired.Value!.CurrentLevel;
        }

        session.RemovePendingMonsterReward(index);
        return Result<MonsterRewardClaimResult>.Success(new MonsterRewardClaimResult(reward, currentLevel));
    }
}
