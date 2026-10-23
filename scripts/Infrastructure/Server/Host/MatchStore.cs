using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace ProjectStar.Server;

// 权威存档与请求回执适配（基础设施层）；候选状态、版本与回执同事务提交。
internal sealed class MatchStore(NpgsqlDataSource source, RulesProcess rules, PvpStore pvp)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // 查询不推进随机或结算；始终限定当前认证玩家。
    public async Task<MatchReply> CurrentAsync(Guid player, CancellationToken ct, Guid? id = null)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        var match = await ReadMatchAsync(connection, null, player, id, ct);
        if (match is null) return Error("match_not_found", 404);
        if (match.RulesVersion != rules.Version) return Error("version_incompatible", 409);
        var reply = await rules.ExecuteAsync(new RulesRequest(Guid.NewGuid(), "view", rules.Version, Save: match.State), ct);
        if (!reply.Ok || reply.View is null) throw new InvalidOperationException("Cannot restore match.");
        return View(match.Id, match.Version, reply.View.Value, 200);
    }

    // 创建与现有对局命令共享每玩家请求ID空间；重复请求先返回原回执。
    public async Task<MatchReply> MutateAsync(Guid player, MatchMutation input, CancellationToken ct)
    {
        // 新增可选命令字段不能改变既有商店请求的摘要，否则升级后原回执无法重试。
        var payload = JsonSerializer.SerializeToNode(input with { RequestId = Guid.Empty }, Json)!;
        if (input.BattleId is null) payload.AsObject().Remove("battleId");
        if (input.RewardIndex is null) payload.AsObject().Remove("rewardIndex");
        if (input.CardId is null) payload.AsObject().Remove("cardId");
        if (input.TargetZone is null) payload.AsObject().Remove("targetZone");
        if (input.TargetStart is null) payload.AsObject().Remove("targetStart");
        if (input.VisitId is null) payload.AsObject().Remove("visitId");
        if (input.OptionKey is null) payload.AsObject().Remove("optionKey");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString(Json))));
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction);
        command.Parameters.AddWithValue(BinaryPrimitives.ReadInt64LittleEndian(player.ToByteArray()));
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.Clear();
        command.CommandText = "SELECT request_hash,http_status,response::text FROM match_requests WHERE player_id=$1 AND request_id=$2";
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(input.RequestId);
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
                return reader.GetString(0).TrimEnd() != hash ? Error("request_id_reused", 409)
                    : new MatchReply(reader.GetInt32(1), reader.GetString(2));
        }
        var match = await ReadMatchAsync(connection, transaction, player, input.MatchId, ct);
        MatchReply result;
        if (input.Action == "create" && match is not null) result = Error("match_in_progress", 409);
        else if (input.Action != "create" && match is null) result = Error("match_not_found", 404);
        else if (match is not null && match.RulesVersion != rules.Version) result = Error("version_incompatible", 409);
        else if (match is not null && match.Version != input.ExpectedVersion) result = Error("version_conflict", 409);
        else if (input.Action == "retry_pvp")
        {
            if (!await pvp.RetryAsync(connection, transaction, player, match!.Id, input.BattleId ?? Guid.Empty, ct))
                result = Error("battle_retry_unavailable", 422);
            else
            {
                var view = await rules.ExecuteAsync(new RulesRequest(Guid.NewGuid(), "view", rules.Version, Save: match.State), ct);
                if (!view.Ok || view.View is null) throw new InvalidOperationException("Cannot restore frozen match.");
                result = View(match.Id, match.Version, view.View.Value, 202);
            }
        }
        else
        {
            var seed = BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(8)).ToString(CultureInfo.InvariantCulture);
            var reply = input.Action == "start_pvp"
                ? await pvp.PrepareAsync(connection, transaction, player, match!.Id, match.Version, match.State, input.EncounterKey!, seed, ct)
                : await rules.ExecuteAsync(new RulesRequest(input.RequestId, input.Action, rules.Version,
                input.HeroKey, seed, match?.State, input.EncounterKey, input.OfferIndex, input.BattleId, input.RewardIndex,
                input.CardId, input.TargetZone, input.TargetStart, input.VisitId, input.OptionKey), ct);
            if (!reply.Ok)
            {
                if (reply.Error == "rules.request_failed") throw new InvalidOperationException("Rule process rejected internal state.");
                result = Error(reply.Error ?? "operation_rejected", 422);
            }
            else
            {
                if (reply.View is null || reply.Save is null || Encoding.UTF8.GetByteCount(reply.Save) > 2 * 1024 * 1024)
                    throw new InvalidOperationException("Invalid candidate state.");
                var id = reply.View.Value.GetProperty("matchId").GetGuid();
                if (match is not null && match.Id != id) throw new InvalidOperationException("Match ID changed.");
                var next = checked((match?.Version ?? 0) + 1);
                command.Parameters.Clear();
                if (match is null)
                {
                    command.CommandText = "INSERT INTO matches(match_id,player_id,version,rules_version,status,state) VALUES ($1,$2,$3,$4,'active',$5::jsonb)";
                    command.Parameters.AddWithValue(id);
                    command.Parameters.AddWithValue(player);
                    command.Parameters.AddWithValue(next);
                    command.Parameters.AddWithValue(rules.Version);
                    command.Parameters.AddWithValue(reply.Save);
                }
                else
                {
                    command.CommandText = "UPDATE matches SET state=$1::jsonb,version=$2,updated_at=now() WHERE match_id=$3 AND player_id=$4 AND version=$5";
                    command.Parameters.AddWithValue(reply.Save);
                    command.Parameters.AddWithValue(next);
                    command.Parameters.AddWithValue(id);
                    command.Parameters.AddWithValue(player);
                    command.Parameters.AddWithValue(match.Version);
                }
                if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("State version changed.");
                result = View(id, next, reply.View.Value, match is null ? 201 : input.Action == "start_pvp" ? 202 : 200);
            }
        }
        command.Parameters.Clear();
        command.CommandText = "INSERT INTO match_requests(player_id,request_id,request_hash,http_status,response) VALUES ($1,$2,$3,$4,$5::jsonb)";
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(input.RequestId);
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(result.Status);
        command.Parameters.AddWithValue(result.Json);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static async Task<StoredMatch?> ReadMatchAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid player, Guid? id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(id is null
            ? "SELECT match_id,version,rules_version,state::text FROM matches WHERE player_id=$1 AND status='active'"
            : "SELECT match_id,version,rules_version,state::text FROM matches WHERE player_id=$1 AND match_id=$2", connection, transaction);
        command.Parameters.AddWithValue(player);
        if (id is not null) command.Parameters.AddWithValue(id.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new StoredMatch(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2).TrimEnd(), reader.GetString(3)) : null;
    }

    private MatchReply View(Guid id, long version, JsonElement view, int status) =>
        new(status, JsonSerializer.Serialize(new { matchId = id, version, rulesVersion = rules.Version, state = view }, Json));
    private static MatchReply Error(string error, int status) => new(status, JsonSerializer.Serialize(new { error }, Json));

    // 数据库已提交状态，不在进程间共享可变领域对象。
    private sealed record StoredMatch(Guid Id, long Version, string RulesVersion, string State);
}

// 规范化操作；只含已确认字段，HTTP 客户端不能设置种子、财富或存档。
internal sealed record MatchMutation(Guid RequestId, string Action, Guid? MatchId = null,
    long? ExpectedVersion = null, string? HeroKey = null, string? EncounterKey = null, int? OfferIndex = null,
    Guid? BattleId = null, int? RewardIndex = null, Guid? CardId = null, string? TargetZone = null, int? TargetStart = null,
    Guid? VisitId = null, string? OptionKey = null);

// 持久化 HTTP 回执，重试返回同一语义结果。
internal sealed record MatchReply(int Status, string Json);

// 创建对局的 HTTP 边界契约。
internal sealed record CreateMatchRequest(Guid RequestId, string? HeroKey);

// 第一批对局命令的 HTTP 边界契约。
internal sealed record MatchCommandRequest(Guid RequestId, long ExpectedMatchVersion, string? Type, string? EncounterKey,
    int? OfferIndex, Guid? BattleId, int? RewardIndex, Guid? CardId, string? TargetZone, int? TargetStart,
    Guid? VisitId, string? OptionKey);
