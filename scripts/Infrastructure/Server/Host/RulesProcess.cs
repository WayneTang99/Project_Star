using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectStar.Server;

// Godot 私有标准流适配（基础设施层）；有界串行执行，失败丢弃进程并从数据库重试。
internal sealed class RulesProcess : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly string _executable = Path.Combine(AppContext.BaseDirectory, "rules", "project-star-rules.x86_64");
    private Process? _process;
    private Task? _stderr;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Version { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rules", "rules-version.txt")).Trim();

    // 检查或重建规则进程；同一进程只接受一个当前请求。
    public async Task EnsureStartedAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { await StartAsync(ct); }
        finally { _gate.Release(); }
    }

    // 每次调用都加载已提交存档，不把工作进程内存当作权威对局。
    public async Task<RulesReply> ExecuteAsync(RulesRequest request, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await _gate.WaitAsync(deadline.Token);
        try
        {
            await StartAsync(deadline.Token);
            await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), deadline.Token);
            await _process.StandardInput.FlushAsync(deadline.Token);
            var line = await _process.StandardOutput.ReadLineAsync(deadline.Token) ?? throw new IOException("Worker exited.");
            if (!line.StartsWith("RULES_RESPONSE=", StringComparison.Ordinal) || line.Length > 4 * 1024 * 1024)
                throw new IOException("Invalid worker response.");
            var reply = JsonSerializer.Deserialize<RulesReply>(line[15..], Json) ?? throw new JsonException();
            if (reply.RequestId != request.RequestId) throw new IOException("Worker response mismatch.");
            return reply;
        }
        catch { await StopAsync(); throw; }
        finally { _gate.Release(); }
    }

    private async Task StartAsync(CancellationToken ct)
    {
        if (_process is { HasExited: false }) return;
        await StopAsync();
        if (Version.Length != 64 || !Version.All(char.IsAsciiHexDigit)) throw new InvalidOperationException("Invalid worker version.");
        await using (var assembly = File.OpenRead(Path.Combine(Path.GetDirectoryName(_executable)!, "data_Project_Star_linuxbsd_x86_64", "Project_Star.dll")))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(assembly, ct));
            if (!actual.Equals(Version, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Worker assembly hash mismatch.");
        }
        var start = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = "/var/lib/project-star",
        };
        foreach (var argument in new[] { "--headless", "--quiet", "--", "--rules-version=" + Version }) start.ArgumentList.Add(argument);
        _process = Process.Start(start) ?? throw new IOException("Cannot start worker.");
        var process = _process;
        // 持续排空标准错误，避免管道阻塞；只保留受控错误类型行。
        _stderr = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
                if (line.StartsWith("RULES_REQUEST_FAILED=", StringComparison.Ordinal) || line.StartsWith("RULES_START_FAILED=", StringComparison.Ordinal))
                    Console.Error.WriteLine(line);
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        for (var count = 0; count < 32; count++)
        {
            var line = await process.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new IOException("Worker startup failed.");
            if (line == "RULES_READY=" + Version) return;
        }
        throw new IOException("Worker readiness missing.");
    }

    private async Task StopAsync()
    {
        if (_process is null) return;
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync();
        if (_stderr is not null)
        {
            try { await _stderr; } catch (IOException) { }
        }
        _process.Dispose();
        _process = null;
        _stderr = null;
    }

    // 宿主退出时终止自有规则子进程。
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _gate.Dispose();
    }
}

// 仅 API 构造的规则请求；不允许 HTTP 调用者上传存档。
internal sealed record RulesRequest(Guid RequestId, string Action, string RulesVersion, string? HeroKey = null,
    string? Seed = null, string? Save = null, string? EncounterKey = null, int? OfferIndex = null,
    Guid? BattleId = null, int? RewardIndex = null, Guid? CardId = null, string? TargetZone = null, int? TargetStart = null,
    Guid? VisitId = null, string? OptionKey = null, string? OpponentSave = null, Guid? OpponentSnapshotId = null);

// 规则候选结果；提交数据库之前不返回客户端。
internal sealed record RulesReply(Guid RequestId, bool Ok, string? Error, string? Save, JsonElement? View);
