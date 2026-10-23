using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ProjectStar.Server;

// 邀请注册、密码和会话规则（基础设施身份模块）；不依赖 Godot 运行时。
internal sealed class AuthService(AuthStore store)
{
    private readonly PasswordHasher<object> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
    private readonly object _user = new();
    private string? _dummyHash;

    // 校验边界并注册内部玩家，不接受客户端提供的玩家 ID。
    public async Task<SessionView?> RegisterAsync(RegisterRequest input, CancellationToken ct)
    {
        var username = NormalizeUsername(input.Username);
        if (username is null || !ValidPassword(input.Password) || !ValidToken(input.Invitation)) return null;
        var player = Guid.NewGuid();
        var (view, write) = NewSession(player);
        var accepted = await store.RegisterAsync(player, username, HashPassword(input.Password!), HashToken(input.Invitation!), write, ct);
        return accepted ? view : null;
    }

    // 同时执行未知账号的虚拟密码校验，对外统一返回认证失败。
    public async Task<SessionView?> LoginAsync(LoginRequest input, CancellationToken ct)
    {
        var username = NormalizeUsername(input.Username);
        if (username is null || !ValidPassword(input.Password)) return null;
        var (view, write) = NewSession(Guid.Empty);
        _dummyHash ??= HashPassword(NewToken());
        var player = await store.LoginAsync(username, hash =>
        {
            var result = _hasher.VerifyHashedPassword(_user, hash ?? _dummyHash, input.Password!);
            if (hash is null || result == PasswordVerificationResult.Failed) return null;
            return result == PasswordVerificationResult.SuccessRehashNeeded ? HashPassword(input.Password!) : hash;
        }, write, ct);
        return player is null ? null : view with { PlayerId = player.Value };
    }

    // 轮换凭证，不延长会话最初的十四天绝对期限。
    public async Task<SessionView?> RefreshAsync(RefreshRequest input, CancellationToken ct)
    {
        if (!ValidToken(input.RefreshToken)) return null;
        var (view, write) = NewSession(Guid.Empty);
        var refreshed = await store.RefreshAsync(HashToken(input.RefreshToken!), write, ct);
        if (refreshed is null) return null;
        var (player, expiry) = refreshed.Value;
        return view with { PlayerId = player, RefreshExpiresAt = expiry,
            AccessExpiresAt = view.AccessExpiresAt < expiry ? view.AccessExpiresAt : expiry };
    }

    // 明确的 ASCII 登录名避免大小写及 Unicode 归一化歧义；展示名另行实现。
    public static string? NormalizeUsername(string? value) =>
        value is { Length: >= 3 and <= 32 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? value.ToLowerInvariant() : null;

    // 密码长度限制同时约束校验成本，不截断或修剪用户密码。
    public static bool ValidPassword(string? value) => value is { Length: >= 10 and <= 128 };

    // 只接受本服务生成的 256 位 base64url 凭证。
    public static bool ValidToken(string? value) => value is { Length: 43 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    // 不可逆凭证摘要用于数据库查找，不使用密码哈希处理高熵随机令牌。
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // 返回一次性可见的随机凭证，调用方负责安全交付。
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // 使用 ASP.NET Core Identity 的独立盐 PBKDF2 密码哈希。
    public string HashPassword(string password) => _hasher.HashPassword(_user, password);

    private static (SessionView View, SessionWrite Write) NewSession(Guid player)
    {
        var access = NewToken();
        var refresh = NewToken();
        var instant = DateTime.UtcNow;
        // PostgreSQL 时间精度为微秒，发给客户端前先与持久化精度对齐。
        var now = new DateTime(instant.Ticks - instant.Ticks % 10, DateTimeKind.Utc);
        var view = new SessionView(player, access, refresh, now.AddMinutes(15), now.AddDays(14));
        return (view, new SessionWrite(Guid.NewGuid(), HashToken(access), HashToken(refresh), view.AccessExpiresAt, view.RefreshExpiresAt));
    }
}
