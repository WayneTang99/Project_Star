using System.Reflection;
using Npgsql;

namespace ProjectStar.Server;

// 账号与会话的 PostgreSQL 适配（基础设施层）；事务保证邀请消耗及凭证撤销的原子性。
internal sealed class AuthStore(NpgsqlDataSource source)
{
    // 显式执行版本迁移；拒绝比当前程序更新的数据库结构。
    public async Task MigrateAsync(CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(1975120341)", connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "CREATE TABLE IF NOT EXISTS schema_versions (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_versions";
        var version = (int)(await command.ExecuteScalarAsync(ct))!;
        if (version > 3) throw new InvalidOperationException("Database schema is newer than this program.");
        if (version == 0)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectStar.Server.Migrations.001_Auth.sql")!;
            using var reader = new StreamReader(stream);
            command.CommandText = await reader.ReadToEndAsync(ct);
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = "INSERT INTO schema_versions(version) VALUES (1)";
            await command.ExecuteNonQueryAsync(ct);
        }
        if (version < 2)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectStar.Server.Migrations.002_Matches.sql")!;
            using var reader = new StreamReader(stream);
            command.CommandText = await reader.ReadToEndAsync(ct);
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = "INSERT INTO schema_versions(version) VALUES (2)";
            await command.ExecuteNonQueryAsync(ct);
        }
        if (version < 3)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectStar.Server.Migrations.003_Pvp.sql")!;
            using var reader = new StreamReader(stream);
            command.CommandText = await reader.ReadToEndAsync(ct);
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = "INSERT INTO schema_versions(version) VALUES (3)";
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    // 检查数据库及当前结构是否可用，不执行迁移。
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        await using var command = source.CreateCommand("SELECT COALESCE(MAX(version), 0) = 3 FROM schema_versions");
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    // 创建邀请码；仅运维命令可调用，数据库不保存原文。
    public async Task CreateInvitationAsync(string hash, int uses, DateTime expiry, CancellationToken ct)
    {
        await using var command = source.CreateCommand("INSERT INTO invitations(code_hash,max_uses,expires_at) VALUES ($1,$2,$3)");
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(uses);
        command.Parameters.AddWithValue(expiry);
        await command.ExecuteNonQueryAsync(ct);
    }

    // 原子注册；失败时账号、会话和邀请码次数全部回滚。
    public async Task<bool> RegisterAsync(Guid player, string username, string passwordHash,
        string invitationHash, SessionWrite session, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE invitations SET uses = uses + 1
            WHERE code_hash=$1 AND uses < max_uses AND expires_at > now()
            """, connection, transaction);
        command.Parameters.AddWithValue(invitationHash);
        if (await command.ExecuteNonQueryAsync(ct) != 1) return false;
        try
        {
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO players(player_id) VALUES ($1)";
            command.Parameters.AddWithValue(player);
            await command.ExecuteNonQueryAsync(ct);
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO password_credentials(player_id,username,password_hash) VALUES ($1,$2,$3)";
            command.Parameters.AddWithValue(player);
            command.Parameters.AddWithValue(username);
            command.Parameters.AddWithValue(passwordHash);
            await command.ExecuteNonQueryAsync(ct);
            await InsertSessionAsync(connection, transaction, player, session, ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    // 密码校验和发放会话持有玩家行锁，避免与重置密码交错授权。
    public async Task<Guid?> LoginAsync(string username, Func<string?, string?> verifyAndRehash,
        SessionWrite session, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT p.player_id,c.password_hash FROM players p
            JOIN password_credentials c USING(player_id)
            WHERE c.username=$1 AND p.enabled FOR UPDATE OF p
            """, connection, transaction);
        command.Parameters.AddWithValue(username);
        Guid? player = null;
        string? hash = null;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct)) { player = reader.GetGuid(0); hash = reader.GetString(1); }
        }
        var acceptedHash = verifyAndRehash(hash);
        if (player is null || acceptedHash is null) return null;
        if (hash != acceptedHash)
        {
            command.Parameters.Clear();
            command.CommandText = "UPDATE password_credentials SET password_hash=$1 WHERE player_id=$2";
            command.Parameters.AddWithValue(acceptedHash);
            command.Parameters.AddWithValue(player.Value);
            await command.ExecuteNonQueryAsync(ct);
        }
        // 清理过期会话，仅影响当前玩家，不使其他仍有效的登录失效。
        command.Parameters.Clear();
        command.CommandText = "DELETE FROM auth_sessions WHERE player_id=$1 AND refresh_expires_at <= now()";
        command.Parameters.AddWithValue(player.Value);
        await command.ExecuteNonQueryAsync(ct);
        await InsertSessionAsync(connection, transaction, player.Value, session, ct);
        await transaction.CommitAsync(ct);
        return player;
    }

    // 单条条件更新轮换凭证；并发续期只能有一个请求成功，旧访问凭证也同时失效。
    public async Task<(Guid Player, DateTime Expiry)?> RefreshAsync(string oldHash, SessionWrite next, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            UPDATE auth_sessions s SET access_hash=$2,refresh_hash=$3,
                access_expires_at=LEAST($4,refresh_expires_at)
            WHERE refresh_hash=$1 AND refresh_expires_at>now()
                AND EXISTS(SELECT 1 FROM players p WHERE p.player_id=s.player_id AND p.enabled)
            RETURNING player_id,refresh_expires_at
            """);
        command.Parameters.AddWithValue(oldHash);
        command.Parameters.AddWithValue(next.AccessHash);
        command.Parameters.AddWithValue(next.RefreshHash);
        command.Parameters.AddWithValue(next.AccessExpiresAt);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetDateTime(1)) : null;
    }

    // 身份只由有效访问凭证取得，调用方不能指定其他玩家 ID。
    public async Task<PlayerView?> GetPlayerAsync(string accessHash, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            SELECT p.player_id,c.username FROM auth_sessions s JOIN players p USING(player_id)
            JOIN password_credentials c USING(player_id)
            WHERE s.access_hash=$1 AND s.access_expires_at>now() AND s.refresh_expires_at>now() AND p.enabled
            """);
        command.Parameters.AddWithValue(accessHash);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new PlayerView(reader.GetGuid(0), reader.GetString(1)) : null;
    }

    // 注销仅撤销提交凭证对应的会话，不能通过请求体选择他人的会话。
    public async Task LogoutAsync(string accessHash, CancellationToken ct)
    {
        await using var command = source.CreateCommand("DELETE FROM auth_sessions WHERE access_hash=$1");
        command.Parameters.AddWithValue(accessHash);
        await command.ExecuteNonQueryAsync(ct);
    }

    // 管理员重置密码并撤销该玩家全部会话；没有公开的免验证找回接口。
    public async Task<Guid?> ResetPasswordAsync(string username, string hash, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT p.player_id FROM players p JOIN password_credentials c USING(player_id)
            WHERE c.username=$1 FOR UPDATE OF p
            """, connection, transaction);
        command.Parameters.AddWithValue(username);
        var player = (Guid?)await command.ExecuteScalarAsync(ct);
        if (player is null) return null;
        command.Parameters.Clear();
        command.CommandText = "UPDATE password_credentials SET password_hash=$1 WHERE player_id=$2";
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(player.Value);
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.Clear();
        command.CommandText = "DELETE FROM auth_sessions WHERE player_id=$1";
        command.Parameters.AddWithValue(player.Value);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return player;
    }

    private static async Task InsertSessionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid player, SessionWrite session, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO auth_sessions(session_id,player_id,access_hash,refresh_hash,access_expires_at,refresh_expires_at)
            VALUES ($1,$2,$3,$4,$5,$6)
            """, connection, transaction);
        command.Parameters.AddWithValue(session.SessionId);
        command.Parameters.AddWithValue(player);
        command.Parameters.AddWithValue(session.AccessHash);
        command.Parameters.AddWithValue(session.RefreshHash);
        command.Parameters.AddWithValue(session.AccessExpiresAt);
        command.Parameters.AddWithValue(session.RefreshExpiresAt);
        await command.ExecuteNonQueryAsync(ct);
    }
}
