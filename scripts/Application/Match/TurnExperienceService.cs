using System;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 在每回合开始发放一次经验，跨轮与重复刷新保持幂等（应用对局层）。
public sealed class TurnExperienceService
{
    // 为尚未结算的当前回合发放1经验，包含第1回合。
    public Result<int> SettleCurrentTurn(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Status != MatchStatus.InProgress) return Result<int>.Success(0);
        if (session.Player.Hero is null)
            return Result<int>.Fail(new Failure(new StringName("experience.missing_hero"), "A hero is required to settle turn experience."));
        var turn = checked((session.Progress.Round - 1) * 8 + session.Progress.Turn);
        if (session.Progress.ExperienceSettledThroughTurn >= turn) return Result<int>.Success(0);
        session.Player.AddExperience(1);
        session.Progress.ExperienceSettledThroughTurn = turn;
        return Result<int>.Success(1);
    }
}
