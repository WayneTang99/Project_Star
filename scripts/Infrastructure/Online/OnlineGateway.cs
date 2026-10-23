using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Online;
using HttpClient = System.Net.Http.HttpClient;

namespace Project_Star.Infrastructure.Online;

// HTTP与内存会话适配；禁止重定向泄露Bearer，不向磁盘或日志输出凭据（基础设施层）。
public sealed class OnlineGateway : IOnlineGateway, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    private string? _access, _refresh;
    private Guid _player;
    public bool IsAuthenticated => _access is not null;

    // 开发仅允许回环HTTP，远程地址要求HTTPS并保留系统证书验证。
    public OnlineGateway(Uri endpoint, HttpMessageHandler? handler = null)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0
            || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/"
            || (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback)))
            throw new ArgumentException("API endpoint must be HTTPS or loopback HTTP.", nameof(endpoint));
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(25), MaxResponseContentBufferSize = 1024 * 1024 };
    }

    // 密码仅用于此请求；成功后只在内存保存服务器返回的当前会话。
    public async Task<OnlineReply> SignInAsync(string username, string password, string? invitation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsAuthenticated) return new(409, "client.already_signed_in");
            var wire = await WireAsync(HttpMethod.Post, invitation is null ? "v1/auth/login" : "v1/auth/register",
                new { username, password, invitation }, false, ct);
            if (wire.Status is >= 200 and < 300)
            {
                try { SaveSession(wire.Body, false); return new(wire.Status, default); }
                catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
                { ClearSession(); return new(0, "client.invalid_response"); }
            }
            return ToReply(wire);
        }
        finally { _gate.Release(); }
    }

    // 本地始终释放凭证，撤销请求失败时不谎称服务器已撤销会话。
    public async Task<OnlineReply> SignOutAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var reply = ToReply(await WireAsync(HttpMethod.Post, "v1/auth/logout", null, true, ct));
            return reply.IsSuccess ? reply : new OnlineReply(reply.Status, "client.logout_uncertain");
        }
        finally { ClearSession(); _gate.Release(); }
    }

    // 当前、归档及任务读取共用会话续期门控。
    public Task<OnlineReply> CurrentAsync(CancellationToken ct) => ReadAsync("v1/matches/current", ct);
    public Task<OnlineReply> MatchAsync(Guid id, CancellationToken ct) => ReadAsync("v1/matches/" + id, ct);
    public Task<OnlineReply> BattleAsync(Guid id, CancellationToken ct) => ReadAsync("v1/battles/" + id, ct);

    // 只编码白名单意图字段；原requestId和预期版本在重试时保持不变。
    public Task<OnlineReply> SendAsync(OnlineCommand command, CancellationToken ct)
    {
        var body = new Dictionary<string, object?> { ["requestId"] = command.RequestId };
        var create = command.Type == "create";
        if (create) body["heroKey"] = command.HeroKey?.ToString();
        else { body["type"] = command.Type.ToString(); body["expectedMatchVersion"] = command.ExpectedVersion; }
        if (command.EncounterKey is { } encounter) body["encounterKey"] = encounter.ToString();
        if (command.OfferIndex is { } offer) body["offerIndex"] = offer;
        if (command.BattleId is { } battle) body["battleId"] = battle;
        if (command.RewardIndex is { } reward) body["rewardIndex"] = reward;
        if (command.VisitId is { } visit) body["visitId"] = visit;
        if (command.OptionKey is { } option) body["optionKey"] = option.ToString();
        if (command.CardId is { } card) body["cardId"] = card;
        if (command.TargetZone is { } zone) body["targetZone"] = zone.ToString();
        if (command.TargetStart is { } start) body["targetStart"] = start;
        return RequestAsync(HttpMethod.Post, create ? "v1/matches" : $"v1/matches/{command.MatchId}/commands", body, ct);
    }

    private Task<OnlineReply> ReadAsync(string path, CancellationToken ct) => RequestAsync(HttpMethod.Get, path, null, ct);
    private async Task<OnlineReply> RequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!IsAuthenticated) return new(401, "authentication_required");
            var wire = await WireAsync(method, path, body, true, ct);
            if (wire.Status == 401 && _refresh is not null)
            {
                var renewed = await WireAsync(HttpMethod.Post, "v1/auth/refresh", new { refreshToken = _refresh }, false, ct);
                if (renewed.Status is >= 200 and < 300)
                {
                    try { SaveSession(renewed.Body, true); }
                    catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
                    { ClearSession(); return new(401, "client.session_lost"); }
                    wire = await WireAsync(method, path, body, true, ct);
                }
                else
                {
                    if (renewed.Status is 0 or 401) { ClearSession(); return new(401, "client.session_lost"); }
                    return ToReply(renewed);
                }
            }
            if (wire.Status == 401) ClearSession();
            return ToReply(wire);
        }
        finally { _gate.Release(); }
    }

    private async Task<WireReply> WireAsync(HttpMethod method, string path, object? body, bool authenticated, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (authenticated && _access is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access);
            if (body is not null)
            {
                request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400) return new(0, default, "client.redirect_rejected");
            if (status == 204) return new(status, default, default);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            using var json = JsonDocument.Parse(bytes);
            return new(status, json.RootElement.Clone(), default);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException or ObjectDisposedException)
        { return new(0, default, "client.network_uncertain"); }
        catch (JsonException) { return new(0, default, "client.invalid_response"); }
    }
    private void SaveSession(JsonElement body, bool rotating)
    {
        var player = body.GetProperty("playerId").GetGuid();
        var access = body.GetProperty("accessToken").GetString();
        var refresh = body.GetProperty("refreshToken").GetString();
        if (player == Guid.Empty || (rotating && player != _player) || access?.Length != 43 || refresh?.Length != 43)
            throw new InvalidOperationException("Invalid session.");
        _player = player; _access = access; _refresh = refresh;
    }
    private void ClearSession() { _access = _refresh = null; _player = Guid.Empty; }
    private static OnlineReply ToReply(WireReply wire)
    {
        if (wire.Status == 0) return new(0, wire.Error);
        if (wire.Status == 204) return new(204, default);
        try
        {
            var body = wire.Body;
            if (wire.Status is < 200 or >= 300) return new(wire.Status, Key(body, "error"));
            if (body.TryGetProperty("state", out _)) return new(wire.Status, default, ReadMatch(body));
            if (body.TryGetProperty("battleId", out _)) return new(wire.Status, default,
                body.TryGetProperty("result", out var result) && result.ValueKind != JsonValueKind.Null ? ReadMatch(result) : null, Key(body, "status"));
            return new(0, "client.invalid_response");
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        { return new(0, "client.invalid_response"); }
    }
    private static OnlineMatch ReadMatch(JsonElement body)
    {
        var id = body.GetProperty("matchId").GetGuid(); var version = body.GetProperty("version").GetInt64();
        var rules = Text(body, "rulesVersion");
        if (id == Guid.Empty || version < 1 || rules.Length != 64 || rules.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidOperationException("Invalid match envelope.");
        var state = body.GetProperty("state");
        return new(id, version, rules, new OnlineState(Key(state, "phase"), Key(state, "status"), Number(state, "round"),
            Number(state, "turn"), Number(state, "wealth"), Number(state, "experience"), Number(state, "reputation"), Number(state, "pvpWins"),
            Rows(state, "choices", x => new OnlineChoice(Key(x, "key"), Text(x, "name"), Key(x, "kind"), Flag(x, "isCardShop"), Flag(x, "isChoiceEvent"))),
            Rows(state, "cards", ReadCard), Rows(state, "pendingRewards", x => new OnlineReward(Number(x, "index"), Text(x, "name"), Number(x, "level"))),
            Optional(state, "shop", x => new OnlineShop(Flag(x, "canRefresh"), Number(x, "refreshCost"), Rows(x, "offers", y =>
                new OnlineOffer(Number(y, "index"), Key(y, "key"), Text(y, "name"), Number(y, "level"), Number(y, "price"), Flag(y, "sold"))))),
            Optional(state, "eventVisit", x => new OnlineVisit(x.GetProperty("id").GetGuid(), Flag(x, "resolved"),
                Rows(x, "options", y => new OnlineOption(Key(y, "key"), Text(y, "name"))))),
            Optional(state, "battle", ReadBattle), Optional(state, "pvp", ReadBattle)));
    }
    private static OnlineCard ReadCard(JsonElement x) => new(x.GetProperty("id").GetGuid(), Key(x, "key"), Text(x, "name"),
        Number(x, "level"), Key(x, "zone"), x.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.Number ? start.GetInt32() : null, Number(x, "value"));
    private static OnlineBattle ReadBattle(JsonElement x)
    {
        var opponent = x.GetProperty("opponent");
        return new(x.GetProperty("id").GetGuid(), Number(x, "round"), x.TryGetProperty("turn", out var turn) ? turn.GetInt32() : 8,
            Text(opponent, "name"), Flag(x, "isTestOpponent"),
            Rows(opponent, "cards", ReadCard), Optional(x, "result", y => new OnlineBattleResult(Key(y, "outcome"),
                Number(y, "playerRemainingHealth"), Number(y, "opponentRemainingHealth"))));
    }
    private static T? Optional<T>(JsonElement x, string field, Func<JsonElement, T> read) where T : class =>
        x.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null ? read(value) : null;
    private static IReadOnlyList<T> Rows<T>(JsonElement x, string field, Func<JsonElement, T> read) =>
        x.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array
            ? Array.AsReadOnly(value.EnumerateArray().Select(read).ToArray()) : Array.Empty<T>();
    private static string Text(JsonElement x, string field) => x.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static StringName Key(JsonElement x, string field) => new(Text(x, field));
    private static int Number(JsonElement x, string field) => x.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
    private static bool Flag(JsonElement x, string field) => x.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True;
    // 仅用于内存解析，禁止将原始Body写入日志或文件（基础设施层）。
    private sealed record WireReply(int Status, JsonElement Body, StringName? Error);
    // 关闭HTTP句柄并释放本次运行的凭证。
    public void Dispose() { ClearSession(); _http.Dispose(); }
}
