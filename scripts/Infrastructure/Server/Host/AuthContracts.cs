namespace ProjectStar.Server;

// 注册请求（基础设施 HTTP 边界）；账号归一化由服务端执行。
internal sealed record RegisterRequest(string? Username, string? Password, string? Invitation);

// 密码登录请求（基础设施 HTTP 边界）。
internal sealed record LoginRequest(string? Username, string? Password);

// 会话续期请求（基础设施 HTTP 边界）。
internal sealed record RefreshRequest(string? RefreshToken);

// 内部玩家身份投影（基础设施账号模块）；不以登录名作为存档主键。
internal sealed record PlayerView(Guid PlayerId, string Username);

// 返回客户端的临时凭证（基础设施账号模块）；数据库只保存摘要。
internal sealed record SessionView(Guid PlayerId, string AccessToken, string RefreshToken,
    DateTime AccessExpiresAt, DateTime RefreshExpiresAt);

// 待持久化会话（基础设施账号模块）。
internal sealed record SessionWrite(Guid SessionId, string AccessHash, string RefreshHash,
    DateTime AccessExpiresAt, DateTime RefreshExpiresAt);
