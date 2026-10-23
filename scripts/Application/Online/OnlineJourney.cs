using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Project_Star.Application.Online;

// 维护单调云端视图与未确定请求；禁止在重试时生成新意图（应用层）。
public sealed class OnlineJourney(IOnlineGateway gateway)
{
    public bool IsAuthenticated => gateway.IsAuthenticated;
    public OnlineMatch? Current { get; private set; }
    public OnlineCommand? Pending { get; private set; }
    public StringName BattleStatus { get; private set; } = new("");
    private bool _busy;

    // 登录成功后读取账号现有对局；密码与邀请码不存入应用状态。
    public async Task<OnlineReply> SignInAsync(string username, string password, string? invitation, CancellationToken ct)
    {
        if (_busy) return Busy();
        _busy = true;
        try
        {
            var reply = await gateway.SignInAsync(username, password, invitation, ct);
            if (!reply.IsSuccess) return reply;
            Current = null; Pending = null; BattleStatus = "";
            reply = await LoadCoreAsync(ct);
            return reply.Status == 404 ? new OnlineReply(200, default) : reply;
        }
        finally { _busy = false; }
    }

    // 退出只撤销当前会话；对局仍由云端保存。
    public async Task<OnlineReply> SignOutAsync(CancellationToken ct)
    {
        if (_busy) return Busy();
        _busy = true;
        try { return await gateway.SignOutAsync(ct); }
        finally { Current = null; Pending = null; BattleStatus = ""; _busy = false; }
    }

    // 主动恢复允许读取其他设备推进后的对局，不提交本地存档。
    public async Task<OnlineReply> LoadAsync(CancellationToken ct)
    {
        if (_busy) return Busy();
        _busy = true;
        try { return await LoadCoreAsync(ct); }
        finally { _busy = false; }
    }

    // 固定请求ID与预期版本；不确定时保留原命令并阻止后续修改。
    public async Task<OnlineReply> SubmitAsync(OnlineCommand command, CancellationToken ct)
    {
        if (_busy || Pending is not null) return Busy();
        if (command.Type != "create" && (Current is not { } current || current.MatchId != command.MatchId || current.Version != command.ExpectedVersion))
            return new OnlineReply(409, "client.stale_view");
        Pending = command.Type == "create" ? command with { MatchId = Current?.MatchId, ExpectedVersion = Current?.Version } : command;
        return await RetryAsync(ct);
    }

    // 完整复用原请求体；历史回执不会覆盖当前较新视图。
    public async Task<OnlineReply> RetryAsync(CancellationToken ct)
    {
        if (_busy || Pending is null) return Busy();
        _busy = true;
        var command = Pending;
        try
        {
            var reply = await gateway.SendAsync(command, ct);
            if (reply.IsSuccess)
            {
                Accept(reply.Match, command.Type == "create" && Current?.MatchId == command.MatchId && Current?.Version == command.ExpectedVersion);
                if (command.Type == "start_pvp" || command.Type == "retry_pvp") BattleStatus = "";
            }
            if (!reply.CanRetry) Pending = null;
            if (reply.Status == 409) await LoadCoreAsync(ct);
            return reply;
        }
        finally { _busy = false; }
    }

    // 查询固定战斗，不调用模拟或再次结算；历史结果按版本单调接受。
    public async Task<OnlineReply> PollAsync(CancellationToken ct)
    {
        if (_busy || Current?.State.Pvp is not { } battle) return Busy();
        _busy = true;
        try
        {
            var reply = await gateway.BattleAsync(battle.Id, ct);
            if (reply.IsSuccess) { BattleStatus = reply.BattleStatus ?? new StringName(""); Accept(reply.Match); }
            return reply;
        }
        finally { _busy = false; }
    }

    private async Task<OnlineReply> LoadCoreAsync(CancellationToken ct)
    {
        var reply = await gateway.CurrentAsync(ct);
        if (reply.Status == 404 && Current is { } previous) reply = await gateway.MatchAsync(previous.MatchId, ct);
        if (reply.IsSuccess) { Accept(reply.Match, true); BattleStatus = ""; }
        return reply;
    }
    private void Accept(OnlineMatch? match, bool allowDifferent = false)
    {
        if (match is null) return;
        if (Current is null || (allowDifferent && Current.MatchId != match.MatchId)
            || (Current.MatchId == match.MatchId && match.Version >= Current.Version)) Current = match;
    }
    private static OnlineReply Busy() => new(409, "client.operation_pending");
}
