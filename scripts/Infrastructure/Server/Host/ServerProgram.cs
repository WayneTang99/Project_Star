using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Npgsql;

namespace ProjectStar.Server;

// 独立 HTTP 与运维入口（基础设施服务宿主）；仅绑定云主机回环地址。
internal static class ServerProgram
{
    // 运行服务或显式运维命令，均要求外置数据库与持久目录配置。
    public static async Task<int> Main(string[] args)
    {
        try
        {
            ValidateDataDirectory();
            var connection = new NpgsqlConnectionStringBuilder(Required("PROJECT_STAR_DATABASE"))
            {
                MaxPoolSize = 20, Timeout = 5, CommandTimeout = 10,
                IncludeErrorDetail = false, LogParameters = false,
                ApplicationName = "project-star-api", GssEncryptionMode = GssEncryptionMode.Disable
            };
            await using var source = NpgsqlDataSource.Create(connection.ConnectionString);
            var store = new AuthStore(source);
            var auth = new AuthService(store);
            if (args.Length > 0) return await AdminAsync(args, store, auth);
            if (!await store.ReadyAsync(CancellationToken.None)) throw new InvalidOperationException("Run migrate before serving.");
            await using var rules = new RulesProcess();
            await rules.EnsureStartedAsync(CancellationToken.None);
            var pvp = new PvpStore(source, rules);
            var matches = new MatchStore(source, rules, pvp);

            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
            builder.Services.AddSingleton(pvp);
            builder.Services.AddHostedService<PvpExecutor>();
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = 16 * 1024;
                options.Listen(IPAddress.Loopback, 5080, listen => listen.Protocols = HttpProtocols.Http1);
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter("password-work", _ => new ConcurrencyLimiterOptions
                    { PermitLimit = 8, QueueLimit = 0 }));
                foreach (var (name, limit) in new[] { ("register", 10), ("login", 20), ("refresh", 60) })
                    options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                        { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
                options.OnRejected = async (context, ct) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    await context.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited" }, ct);
                };
            });
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                try { await next(context); }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
                catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception)
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = exception.StatusCode;
                        await context.Response.WriteAsJsonAsync(new { error = "invalid_request" });
                    }
                }
                catch (Exception exception)
                {
                    // 不记录异常正文，防止数据库参数或凭证进入日志。
                    app.Logger.LogError("Request failed: {ErrorType}; trace={Trace}", exception.GetType().Name, context.TraceIdentifier);
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 503;
                        await context.Response.WriteAsJsonAsync(new { error = "temporarily_unavailable" });
                    }
                }
            });
            app.UseRateLimiter();
            app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
            app.MapGet("/health/ready", async (CancellationToken ct) =>
            {
                await rules.EnsureStartedAsync(ct);
                return await store.ReadyAsync(ct) ? (IResult)Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
            });
            app.MapPost("/v1/auth/register", async (RegisterRequest input, CancellationToken ct) =>
            {
                var session = await auth.RegisterAsync(input, ct);
                if (session is null) return Error("registration_rejected", 400);
                app.Logger.LogInformation("Account registered: {PlayerId}", session.PlayerId);
                return Results.Json(session, statusCode: 201);
            }).RequireRateLimiting("register");
            app.MapPost("/v1/auth/login", async (LoginRequest input, CancellationToken ct) =>
            {
                var session = await auth.LoginAsync(input, ct);
                return session is null ? Error("authentication_failed", 401) : Results.Ok(session);
            }).RequireRateLimiting("login");
            app.MapPost("/v1/auth/refresh", async (RefreshRequest input, CancellationToken ct) =>
            {
                var session = await auth.RefreshAsync(input, ct);
                return session is null ? Error("authentication_failed", 401) : Results.Ok(session);
            }).RequireRateLimiting("refresh");
            app.MapPost("/v1/auth/logout", async (HttpContext context, CancellationToken ct) =>
            {
                var access = ReadBearer(context);
                if (access is null) return Error("authentication_required", 401);
                await store.LogoutAsync(AuthService.HashToken(access), ct);
                return Results.NoContent();
            });
            app.MapGet("/v1/account", async (HttpContext context, CancellationToken ct) =>
            {
                var access = ReadBearer(context);
                var player = access is null ? null : await store.GetPlayerAsync(AuthService.HashToken(access), ct);
                return player is null ? Error("authentication_required", 401) : Results.Ok(player);
            });
            app.MapGet("/v1/matches/current", async (HttpContext context, CancellationToken ct) =>
            {
                var player = await AuthenticatedAsync(context, store, ct);
                if (player is null) return Error("authentication_required", 401);
                var reply = await matches.CurrentAsync(player.PlayerId, ct);
                return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
            });
            app.MapPost("/v1/matches", async (CreateMatchRequest input, HttpContext context, CancellationToken ct) =>
            {
                var player = await AuthenticatedAsync(context, store, ct);
                if (player is null) return Error("authentication_required", 401);
                if (input.RequestId == Guid.Empty || input.HeroKey is not { Length: > 0 and <= 100 }) return Error("invalid_request", 400);
                var reply = await matches.MutateAsync(player.PlayerId,
                    new MatchMutation(input.RequestId, "create", HeroKey: input.HeroKey), ct);
                return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
            });
            app.MapPost("/v1/matches/{id:guid}/commands", async (Guid id, MatchCommandRequest input, HttpContext context, CancellationToken ct) =>
            {
                var player = await AuthenticatedAsync(context, store, ct);
                if (player is null) return Error("authentication_required", 401);
                if (id == Guid.Empty || input.RequestId == Guid.Empty || input.ExpectedMatchVersion < 1
                    || input.Type is not ("select_shop" or "buy" or "leave_shop" or "select_monster" or "resolve_monster" or "claim_monster_reward" or "finish_monster" or "move_card" or "sell_card" or "refresh_shop" or "select_event" or "resolve_event" or "leave_event" or "start_pvp" or "finish_pvp" or "claim_reward" or "retry_pvp")
                    || (input.Type is "select_shop" or "select_monster" or "select_event" or "start_pvp" && input.EncounterKey is not { Length: > 0 and <= 100 })
                    || (input.Type is "resolve_monster" or "claim_monster_reward" or "finish_monster" or "finish_pvp" or "claim_reward" or "retry_pvp" && (input.BattleId is null || input.BattleId == Guid.Empty))
                    || (input.Type is "claim_monster_reward" or "claim_reward" && input.RewardIndex is not (>= 0 and <= 255))
                    || (input.Type is "move_card" or "sell_card" && (input.CardId is null || input.CardId == Guid.Empty))
                    || (input.Type == "move_card" && (input.TargetZone is not ("Battlefield" or "Bench") || input.TargetStart is not (>= 0 and <= 9)))
                    || (input.Type is "resolve_event" or "leave_event" && (input.VisitId is null || input.VisitId == Guid.Empty))
                    || (input.Type == "resolve_event" && input.OptionKey is not { Length: > 0 and <= 100 })
                    || (input.Type == "buy" && input.OfferIndex is not (>= 0 and <= 2))) return Error("invalid_request", 400);
                var mutation = new MatchMutation(input.RequestId, input.Type, id, input.ExpectedMatchVersion,
                    EncounterKey: input.Type is "select_shop" or "select_monster" or "select_event" or "start_pvp" ? input.EncounterKey : null,
                    OfferIndex: input.Type == "buy" ? input.OfferIndex : null,
                    BattleId: input.Type is "resolve_monster" or "claim_monster_reward" or "finish_monster" or "finish_pvp" or "claim_reward" or "retry_pvp" ? input.BattleId : null,
                    RewardIndex: input.Type is "claim_monster_reward" or "claim_reward" ? input.RewardIndex : null,
                    CardId: input.Type is "move_card" or "sell_card" ? input.CardId : null,
                    TargetZone: input.Type == "move_card" ? input.TargetZone : null,
                    TargetStart: input.Type == "move_card" ? input.TargetStart : null,
                    VisitId: input.Type is "resolve_event" or "leave_event" ? input.VisitId : null,
                    OptionKey: input.Type == "resolve_event" ? input.OptionKey : null);
                var reply = await matches.MutateAsync(player.PlayerId, mutation, ct);
                return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
            });
            app.MapGet("/v1/matches/{id:guid}", async (Guid id, HttpContext context, CancellationToken ct) =>
            {
                var player = await AuthenticatedAsync(context, store, ct);
                if (player is null) return Error("authentication_required", 401);
                var reply = await matches.CurrentAsync(player.PlayerId, ct, id);
                return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
            });
            app.MapGet("/v1/battles/{id:guid}", async (Guid id, HttpContext context, CancellationToken ct) =>
            {
                var player = await AuthenticatedAsync(context, store, ct);
                if (player is null) return Error("authentication_required", 401);
                var reply = await pvp.ReadAsync(player.PlayerId, id, ct);
                return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
            });
            await app.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Server command failed ({exception.GetType().Name}). Check configuration, database and schema version.");
            return 1;
        }
    }

    private static async Task<int> AdminAsync(string[] args, AuthStore store, AuthService auth)
    {
        var ct = CancellationToken.None;
        switch (args[0])
        {
            case "migrate" when args.Length == 1:
                await store.MigrateAsync(ct);
                Console.WriteLine("Schema ready: 3");
                return 0;
            case "create-invite" when args.Length == 3
                && int.TryParse(args[1], CultureInfo.InvariantCulture, out var uses) && uses is >= 1 and <= 1000
                && int.TryParse(args[2], CultureInfo.InvariantCulture, out var days) && days is >= 1 and <= 90:
                var token = AuthService.NewToken();
                await store.CreateInvitationAsync(AuthService.HashToken(token), uses, DateTime.UtcNow.AddDays(days), ct);
                // 此命令唯一的标准输出是邀请码；不要把输出接入业务日志。
                Console.WriteLine(token);
                return 0;
            case "reset-password" when args.Length == 2:
                var username = AuthService.NormalizeUsername(args[1]);
                var password = await Console.In.ReadLineAsync(ct);
                if (username is null || !AuthService.ValidPassword(password)) return 2;
                var player = await store.ResetPasswordAsync(username, auth.HashPassword(password!), ct);
                if (player is null) return 3;
                Console.WriteLine($"Password reset; all sessions revoked: {player}");
                return 0;
            default:
                Console.Error.WriteLine("Usage: [migrate | create-invite USES DAYS | reset-password USERNAME (password on stdin)]");
                return 2;
        }
    }

    private static IResult Error(string error, int status) => Results.Json(new { error }, statusCode: status);

    private static async Task<PlayerView?> AuthenticatedAsync(HttpContext context, AuthStore store, CancellationToken ct)
    {
        var access = ReadBearer(context);
        return access is null ? null : await store.GetPlayerAsync(AuthService.HashToken(access), ct);
    }

    private static string? ReadBearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..];
        return AuthService.ValidToken(token) ? token : null;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing {name}.");

    private static void ValidateDataDirectory()
    {
        var path = Required("PROJECT_STAR_DATA_DIR");
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new InvalidOperationException("Data directory must exist and be absolute.");
        var directory = new DirectoryInfo(path);
        var canonical = Path.TrimEndingDirectorySeparator((directory.ResolveLinkTarget(true) ?? directory).FullName);
        // 首版仅允许明确的系统持久目录，拒绝源码、程序、output 及其旁边的相对路径。
        if (canonical != "/var/lib/project-star") throw new InvalidOperationException("Data directory must be /var/lib/project-star.");
    }
}
