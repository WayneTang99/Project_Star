# 账号服务：当前实现与云端运行

更新：2026-10-23。账号后端、权威存档及真实快照PvP已在云端验证，见[权威存档](MATCH_PERSISTENCE.md)与[PvP实现](PVP_SERVER.md)。Main已接[联网操作面板](ONLINE_CLIENT.md)的注册／登录、内存会话、恢复与首轮PvP继续；HTTPS、Steam、完整棋盘及回放仍待办。账号首轮证据见[P1记录](../quality/ASYNC_PVP_P1_2026_10_23.md)，最新服务部署及52项账号／程序更新回归见[P4记录](../quality/ASYNC_PVP_P4_2026_10_23.md)。

## 1. 身份与边界

- 已确认当前测试采用邀请码注册、账号密码登录；每个内部玩家只允许一局进行中的正式对局，P2已通过数据库唯一索引和创建事务落实。
- `players.player_id` 是服务器生成的 UUID，之后的存档、战绩和对局都引用它。`password_credentials` 独立保存登录名和密码哈希，不以登录名、昵称或 SteamID 作为存档主键。
- 登录名为 3～32 位 ASCII 字母、数字或下划线，统一小写；密码为 10～128 个字符，不修剪、不截断。展示名尚未实现。
- 宿主在 `scripts/Infrastructure/Server/Host/`，使用 ASP.NET Core / .NET 10 LTS、Npgsql 10.0.3 和 PostgreSQL；不引用 Godot 程序集，不创建 `StringName`。HTTP 路径、错误码、SQL 字段是基础设施边界文本；游戏领域 key 仍沿用 `StringName`。
- 宿主通过 `.gdignore` 和根工程 `Compile Remove` 与 Godot 隔离，不加入游戏解决方案。游戏仍为 Godot 4.7.2 / net8.0，不要求客户端开发环境立即安装 .NET 10。
- 密码使用 ASP.NET Core Identity PBKDF2 独立盐哈希，当前迭代 210,000 次；登录遇到旧成本哈希时支持重哈希。未知账号同样执行虚拟密码校验。

## 2. 会话契约

访问凭证与续期凭证分别为 32 字节安全随机数的 base64url 文本，长度 43。数据库只存 SHA-256 摘要；没有依赖发布目录的签名密钥。访问有效期 15 分钟，会话绝对期限 14 天，续期不延长此期限。

续期原子替换两种凭证，旧访问及续期凭证随即失效；并发续期只有一个成功。客户端应串行续期，再统一替换保存的凭证，不能让多个请求同时刷新。退出撤销对应会话；管理员重置密码撤销该玩家全部会话。允许同一账号有多个登录会话，“每账号一局”不等于“每账号只能登录一台设备”。

退出可用仍对应当前会话的访问凭证撤销该会话，即使访问期限已经到期；已轮换的旧凭证不能撤销新会话。客户端登录面板已接入，凭证只在内存，关闭程序后重登恢复；系统凭证库及记住登录仍待接。HTTP适配串行续期，不记录凭据；测试脚本同样只在内存保存凭证。

| 接口 | 输入／认证 | 成功结果 |
|---|---|---|
| `POST /v1/auth/register` | JSON：`username`、`password`、`invitation` | 201，会话 |
| `POST /v1/auth/login` | JSON：`username`、`password` | 200，会话 |
| `POST /v1/auth/refresh` | JSON：`refreshToken` | 200，新会话凭证 |
| `POST /v1/auth/logout` | `Authorization: Bearer <accessToken>` | 204；重复退出幂等 |
| `GET /v1/account` | 同上 | `playerId`、归一化 `username` |
| `GET /health/live` | 无 | 进程存活 |
| `GET /health/ready` | 无 | 数据库结构版本可用；失败为 503 |

会话响应字段：`playerId`、`accessToken`、`refreshToken`、`accessExpiresAt`、`refreshExpiresAt`。时间为 UTC ISO 8601，与 PostgreSQL 微秒精度对齐。请求中的 `playerId` 或自定义身份头不能改变认证身份。

失败响应为 `{"error":"错误码"}`：注册失败统一 `registration_rejected` / 400；登录和续期失败统一 `authentication_failed` / 401；缺少或无效访问凭证为 `authentication_required` / 401；非法请求 400、超大请求 413、限流 429、内部或数据库暂时失败 503。不返回账号是否存在、SQL 或凭据。

所有响应使用 `Cache-Control: no-store`。请求体上限 16 KiB；注册、登录、续期分别限制每来源 IP 每分钟 10、20、60 次；全局最多 8 个处理中的请求，不排队。Kestrel 及数据库连接／命令保持有界超时；数据库连接池上限 20。限流状态为进程内状态，重启会清空，不构成 500 人容量证据。

目前只监听 `127.0.0.1:5080`，不读取客户端伪造的转发 IP 头。以后接反向代理时，必须限制可信代理并正确恢复来源 IP，再验证限流；否则所有公网玩家会共用回环地址额度。公网开放还需要 HTTPS、证书与防火墙验收。

## 3. 云端目录与配置

| 内容 | 当前位置 |
|---|---|
| 当前API及规则程序 | `/opt/project-star/releases/p4-20261023-pvp-01/host/`，root所有、服务账号只读；规则包在host/rules/ |
| 当前账号程序入口 | `/opt/project-star/api-current`，独立于 P0 诊断入口 |
| 持久业务目录 | `/var/lib/project-star/`，服务账号持有；不得指向程序、源码或 output |
| 数据库 | `project_star`，物理目录 `/var/lib/postgresql/18/main/` |
| 配置 | `/etc/project-star/api.env`，root:project-star，0640 |
| 日志 | systemd journal，`journalctl -u project-star-api`；预留 `/var/log/project-star/api/` |
| 服务 | `project-star-api.service`，系统账号 `project-star`，禁止交互登录 |
| 构建工具／源码副本 | `/home/ubuntu/project-star-tools/`，不是玩家数据目录 |

配置示例不含密码或 Steam 密钥：

```bash
PROJECT_STAR_DATA_DIR=/var/lib/project-star
PROJECT_STAR_DATABASE="Host=/var/run/postgresql;Database=project_star;Username=project-star"
DOTNET_ENVIRONMENT=Production
```

数据库通过 Unix socket peer 认证，服务系统账号与 PostgreSQL 角色同名；不在客户端或发布包保存数据库密码。PostgreSQL TCP 端口仅监听本机。首版运行时要求持久目录明确为 `/var/lib/project-star`，缺失、相对路径或其他目录均启动失败。当前账号内容全部保存在 PostgreSQL，此目录保留备份和后续文件数据。

服务使用 systemd 自动恢复、只读系统目录、独立临时目录、512 MiB 内存上限和 200% CPU 配额。账号日志只记录必要玩家 ID 或错误类型／追踪号，不记录请求体、Authorization、密码、邀请码、令牌或完整数据库异常。管理员重置命令输出玩家 ID，用于留存运维记录；操作者的身份核验与后续离机审计仍需运维流程落实。

## 4. 构建、迁移与更新

宿主需要 .NET 10 SDK，当前云端工具为 `/home/ubuntu/project-star-tools/dotnet/dotnet`，版本 10.0.402；下载时已校验微软官方发布元数据中的 SHA-512。下面命令在云端构建，不使用本机 WSL：

```bash
/home/ubuntu/project-star-tools/dotnet/dotnet publish \
  /home/ubuntu/project-star-tools/pvp-source/scripts/Infrastructure/Server/Host/ProjectStar.Server.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o /home/ubuntu/project-star-tools/account-publish

bash /home/ubuntu/project-star-tools/InstallAccountHost.sh \
  /home/ubuntu/project-star-tools/account-publish p4-YYYYMMDD-pvp-NN
```

使用前将仓库当前源码及安装脚本同步到以上构建位置，不复用过期源码；最新隔离源码为`/home/ubuntu/project-star-tools/pvp-source/`。P2开始还必须将同构建规则包放入account-publish/rules/，见[权威存档](MATCH_PERSISTENCE.md#6-构建与当前部署)。本轮发布暂存为pvp-publish/，已同步到通用account-publish/供既有维护脚本使用。示例版本名替换为新的p4版本。发布目录不可覆盖；安装脚本创建程序版本、保留配置、验证最终规则包、显式迁移，再切换入口和检查就绪。当前结构版本为3，存档格式为4；普通服务启动不自动改结构，P1及结构2旧程序不能当作当前回滚目标。

运行`VerifyAccountHost.py`维护回归前，将它和当前`InstallAccountHost.sh`一起复制到`/home/ubuntu/project-star-tools/`，并先把当前完整宿主／规则包同步到同目录的`account-publish/`。脚本按自身位置寻找这些同级工件，不能直接从深层源码目录启动而假定会使用外层暂存包。怪物／商店验证脚本可与同级VerifyAccountHost模块一起从隔离源码目录运行。

回滚只切换兼容的程序，不回滚数据库。安装器记录迁移前后结构版本；同结构就绪失败可恢复旧程序，跨结构迁移后的就绪失败保留新程序并要求向前修复，避免自动运行不兼容旧程序。不能把恢复旧程序当作恢复数据。清理旧程序只能针对明确的 `/opt/project-star/releases/<旧版本>`，不能清理共同父目录；本次52项维护检查已核对同规则程序更新、回滚、删除临时程序后会话、对局、PvP输入／结果及快照仍保留。跨结构就绪故障分支本轮仅作源码检查，未在实际数据库注入该故障。

现有 GitHub Release 流水线仍输出 P0 Godot 诊断包和 Windows 客户端，**尚未加入本账号宿主**。本轮采用显式云端发布，源码分支只保存代码、脚本和文档。后续接入流水线时把宿主加入 Linux Release 附件，仍不把二进制放进代码分支。

## 5. 管理命令

通过 SSH 后，在有 sudo 权限的终端调用；私钥不进入仓库，连接方法见 [SERVER_ACCESS](SERVER_ACCESS.md)。

创建一个可使用 1 次、1 天内有效的邀请码（原文只输出这一次，请安全交付给测试者）：

```bash
sudo -u project-star bash -c 'set -a; source /etc/project-star/api.env; set +a; exec /opt/project-star/api-current/host/ProjectStar.Server create-invite 1 1'
```

受控重置密码（先由管理员线下核实账号归属；不要把密码写进命令参数、聊天、shell 历史或文档）：

```bash
read -rsp 'New password: ' account_password; printf '\n'
printf '%s\n' "$account_password" | sudo -u project-star bash -c 'set -a; source /etc/project-star/api.env; set +a; exec /opt/project-star/api-current/host/ProjectStar.Server reset-password USERNAME'
unset account_password
```

邀请码最多 1000 次、有效期 1～90 天；无需账户注册的公开管理员接口不存在。此阶段不提供邮件找回。

## 6. 验证与备份

云端运行 `python3 /home/ubuntu/project-star-tools/VerifyAccountHost.py`，需要 sudo、当前已部署服务，以及同目录的 `InstallAccountHost.sh` / `account-publish/`。它会重启／切换服务、生成 `qa_` 前缀测试玩家、测试并发与限流、在独立恢复数据库检查备份，再删除自己创建的恢复库和临时程序版本。仅在开发维护窗口运行，不用于有真实玩家的日常健康探测。

测试玩家与对应会话保留在独立 PostgreSQL；日志只输出检查结果，不输出凭据。自定义格式备份放在 `/var/lib/project-star/backups/`，文件权限 0600。备份当前仍位于同一台服务器，离机备份与定时保留策略尚未配置；这不能防止整机或磁盘损坏。

## 7. Steam 与成就接入顺序

用户已同意客户端优先验证 Steamworks.NET，复用其账号信息、认证票据和成就接口。当前没有下载或安装 Steam SDK，也没有假定已经有正式 AppID。

1. 取得项目 Steam AppID，验证 Godot 4.7.2 / C# 的初始化、回调、关闭和 Windows 导出依赖。
2. 客户端读取 SteamID、昵称、头像，并申请 Web API 认证票据；这些展示信息不直接授予云端身份。
3. 云端验证票据与目标 AppID／访问资格，再查找或创建内部玩家；SteamID 的外部关联必须唯一。绑定已有测试账号时要求当前账号会话与 Steam 票据双重证明，不能凭昵称合并。
4. Steam 登录与密码登录共用内部玩家 ID 和本服务的会话。只通过 Steam 创建的玩家不强制设置密码；相应身份查询随正式接入调整，目前仅实现密码账号。
5. 在 Steamworks 后台配置成就，确认服务器判定的对局事实与成就条件后，再同步到 Steam；断线补交和已解锁状态按具体成就需求实现，不预建完整框架。

Steam Web API 密钥只放云端外置配置，不进客户端、仓库或 Release。成就接入安排在 PvP 切片之后、Steam 版本发布之前，不阻挡当前账号与存档主线。

依据：[Steamworks.NET](https://steamworks.github.io/)、[Steam 身份认证](https://partner.steamgames.com/doc/features/auth)、[Steam 成就流程](https://partner.steamgames.com/doc/features/achievements/ach_guide)、[.NET 支持周期](https://dotnet.microsoft.com/en-us/platform/support/policy)。
