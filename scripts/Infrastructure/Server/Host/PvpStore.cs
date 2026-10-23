using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace ProjectStar.Server;

// 固定快照匹配、任务租约与一次结算适配（基础设施层）；数据库是唯一任务事实来源。
internal sealed class PvpStore(NpgsqlDataSource source, RulesProcess rules)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // 在参战事务内固定对手与输入；实际模拟由事务提交后的后台执行器完成。
    public async Task<RulesReply> PrepareAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid player, Guid matchId, long version, string state, string encounterKey, string seed, CancellationToken ct)
    {
        using var original = JsonDocument.Parse(state);
        var round = original.RootElement.GetProperty("round").GetInt32();
        await using var command = new NpgsqlCommand("""
            SELECT s.snapshot_id,s.state FROM pvp_snapshots s
            WHERE s.rules_version=$1 AND s.round=$2 AND s.player_id<>$3
            ORDER BY EXISTS(SELECT 1 FROM pvp_battles b JOIN pvp_snapshots previous ON previous.snapshot_id=b.opponent_snapshot_id
                WHERE b.match_id=$4 AND previous.player_id=s.player_id), random() LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue(rules.Version);
        command.Parameters.AddWithValue(round);
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(matchId);
        Guid? snapshotId = null;
        string? opponent = null;
        await using (var reader = await command.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct)) { snapshotId = reader.GetGuid(0); opponent = reader.GetString(1); }
        var battleId = Guid.NewGuid();
        var reply = await rules.ExecuteAsync(new RulesRequest(Guid.NewGuid(), "prepare_pvp", rules.Version,
            Seed: seed, Save: state, EncounterKey: encounterKey, BattleId: battleId, OpponentSave: opponent, OpponentSnapshotId: snapshotId), ct);
        if (!reply.Ok) return reply;
        if (reply.Save is null || reply.View is null || Encoding.UTF8.GetByteCount(reply.Save) > 2 * 1024 * 1024
            || reply.View.Value.GetProperty("pvp").GetProperty("id").GetGuid() != battleId)
            throw new InvalidOperationException("Invalid frozen PvP input.");
        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO pvp_battles(battle_id,player_id,match_id,round,rules_version,opponent_snapshot_id,is_test_opponent,
                published_snapshot_id,player_snapshot,input_state,input_sha256,expected_match_version)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            """;
        command.Parameters.AddWithValue(battleId);
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(matchId);
        command.Parameters.AddWithValue(round);
        command.Parameters.AddWithValue(rules.Version);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)snapshotId ?? DBNull.Value });
        command.Parameters.AddWithValue(snapshotId is null);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(state);
        command.Parameters.AddWithValue(reply.Save);
        command.Parameters.AddWithValue(Hash(reply.Save));
        command.Parameters.AddWithValue(checked(version + 1));
        await command.ExecuteNonQueryAsync(ct);
        return reply;
    }

    // 身份隔离读取摘要；不返回随机状态、冻结存档、任务令牌或其他玩家账号身份。
    public async Task<MatchReply> ReadAsync(Guid player, Guid id, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            SELECT rules_version,status,round,attempts,is_test_opponent,opponent_snapshot_id,result::text
            FROM pvp_battles WHERE battle_id=$1 AND player_id=$2
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(player);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new MatchReply(404, "{\"error\":\"battle_not_found\"}");
        if (reader.GetString(0).TrimEnd() != rules.Version) return new MatchReply(409, "{\"error\":\"version_incompatible\"}");
        return new MatchReply(200, JsonSerializer.Serialize(new
        {
            battleId = id, status = reader.GetString(1), round = reader.GetInt32(2), attempts = reader.GetInt32(3),
            isTestOpponent = reader.GetBoolean(4), opponentSnapshotId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5),
            result = reader.IsDBNull(6) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(reader.GetString(6)),
        }, Json));
    }

    // 技术失败可重试原任务；输入、对手、种子、预期版本均保持不变。
    public async Task<bool> RetryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid player, Guid matchId, Guid battleId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE pvp_battles b SET status='pending',attempts=0,error=NULL,next_attempt_at=now()
            FROM matches m WHERE b.battle_id=$1 AND b.player_id=$2 AND b.match_id=$3 AND b.rules_version=$4
            AND b.status='failed' AND m.match_id=b.match_id AND m.version=b.expected_match_version AND m.state=b.input_state::jsonb
            """, connection, transaction);
        command.Parameters.AddWithValue(battleId);
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(matchId);
        command.Parameters.AddWithValue(rules.Version);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    // 单条原子领取配合SKIP LOCKED；租约过期可回收，最多三次自动尝试。
    public async Task<PvpJob?> ClaimAsync(CancellationToken ct)
    {
        await using var expire = source.CreateCommand("""
            UPDATE pvp_battles SET status='failed',error='attempts_exhausted',lease_token=NULL,lease_until=NULL
            WHERE rules_version=$1 AND status='running' AND lease_until<now() AND attempts>=3
            """);
        expire.Parameters.AddWithValue(rules.Version);
        await expire.ExecuteNonQueryAsync(ct);
        await using var command = source.CreateCommand("""
            WITH candidate AS (SELECT battle_id FROM pvp_battles WHERE rules_version=$1 AND attempts<3
                AND ((status='pending' AND next_attempt_at<=now()) OR (status='running' AND lease_until<now()))
                ORDER BY created_at,battle_id FOR UPDATE SKIP LOCKED LIMIT 1)
            UPDATE pvp_battles b SET status='running',attempts=attempts+1,lease_token=$2,lease_until=now()+interval '60 seconds'
            FROM candidate WHERE b.battle_id=candidate.battle_id
            RETURNING b.battle_id,b.player_id,b.match_id,b.lease_token,b.input_state,b.input_sha256,b.expected_match_version
            """);
        command.Parameters.AddWithValue(rules.Version);
        command.Parameters.AddWithValue(Guid.NewGuid());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new PvpJob(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
            reader.GetString(4), reader.GetString(5).TrimEnd(), reader.GetInt64(6)) : null;
    }

    // 模拟不持有SQL事务；提交时再校验租约、令牌、输入、版本，并原子发布开战前快照。
    public async Task ExecuteAsync(PvpJob job, CancellationToken ct)
    {
        if (Hash(job.Input) != job.Hash) throw new InvalidOperationException("Frozen input hash mismatch.");
        var reply = await rules.ExecuteAsync(new RulesRequest(Guid.NewGuid(), "resolve_pvp", rules.Version, Save: job.Input, BattleId: job.Id), ct);
        if (!reply.Ok || reply.View is null || reply.Save is null || Encoding.UTF8.GetByteCount(reply.Save) > 2 * 1024 * 1024
            || reply.View.Value.GetProperty("phase").GetString() != "PvpResult"
            || reply.View.Value.GetProperty("pvp").GetProperty("id").GetGuid() != job.Id)
            throw new InvalidOperationException("PvP rule execution failed.");
        var status = reply.View.Value.GetProperty("status").GetString();
        if (status is not ("InProgress" or "Won" or "Lost")) throw new InvalidOperationException("Invalid match status.");
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction);
        command.Parameters.AddWithValue(BinaryPrimitives.ReadInt64LittleEndian(job.Player.ToByteArray()));
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.Clear();
        command.CommandText = """
            SELECT battle_id FROM pvp_battles WHERE battle_id=$1 AND status='running' AND lease_token=$2
                AND lease_until>now() AND input_sha256=$3 FOR UPDATE
            """;
        command.Parameters.AddWithValue(job.Id);
        command.Parameters.AddWithValue(job.Token);
        command.Parameters.AddWithValue(job.Hash);
        if (await command.ExecuteScalarAsync(ct) is null) return; // 旧执行器不提交，也不修改新租约。
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE matches SET state=$1::jsonb,version=version+1,status=$2,updated_at=now()
            WHERE match_id=$3 AND player_id=$4 AND version=$5 AND state=$6::jsonb AND status='active'
            """;
        command.Parameters.AddWithValue(reply.Save);
        command.Parameters.AddWithValue(status == "InProgress" ? "active" : "ended");
        command.Parameters.AddWithValue(job.Match);
        command.Parameters.AddWithValue(job.Player);
        command.Parameters.AddWithValue(job.Version);
        command.Parameters.AddWithValue(job.Input);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Frozen match version changed.");
        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO pvp_snapshots(snapshot_id,player_id,match_id,round,rules_version,state)
            SELECT published_snapshot_id,player_id,match_id,round,rules_version,player_snapshot FROM pvp_battles WHERE battle_id=$1
            """;
        command.Parameters.AddWithValue(job.Id);
        await command.ExecuteNonQueryAsync(ct);
        var result = JsonSerializer.Serialize(new { matchId = job.Match, version = job.Version + 1, rulesVersion = rules.Version, state = reply.View.Value }, Json);
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE pvp_battles SET status='completed',result=$1::jsonb,completed_at=now(),lease_token=NULL,lease_until=NULL,error=NULL
            WHERE battle_id=$2 AND status='running' AND lease_token=$3
            """;
        command.Parameters.AddWithValue(result);
        command.Parameters.AddWithValue(job.Id);
        command.Parameters.AddWithValue(job.Token);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Task lease changed.");
        await transaction.CommitAsync(ct);
    }

    // 失败不判玩家输；只更新本执行令牌，保持原输入等待有界重试或人工触发重试。
    public async Task FailAsync(PvpJob job, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            UPDATE pvp_battles SET status=CASE WHEN attempts>=3 THEN 'failed' ELSE 'pending' END,
                error='execution_failed',lease_token=NULL,lease_until=NULL,next_attempt_at=now()+interval '1 second'
            WHERE battle_id=$1 AND status='running' AND lease_token=$2
            """);
        command.Parameters.AddWithValue(job.Id);
        command.Parameters.AddWithValue(job.Token);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

// 单次租约执行凭据；不向HTTP客户端提供（基础设施层）。
internal sealed record PvpJob(Guid Id, Guid Player, Guid Match, Guid Token, string Input, string Hash, long Version);

// 常驻有界执行器；服务重启后重新扫描数据库，用户断线不会取消已提交任务（基础设施层）。
internal sealed class PvpExecutor(PvpStore store, ILogger<PvpExecutor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await store.ClaimAsync(stoppingToken);
                if (job is null) { await Task.Delay(500, stoppingToken); continue; }
                try { await store.ExecuteAsync(job, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    logger.LogWarning("PvP execution failed: {ErrorType}; battle={BattleId}", exception.GetType().Name, job.Id);
                    await store.FailAsync(job, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError("PvP queue failed: {ErrorType}", exception.GetType().Name);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
