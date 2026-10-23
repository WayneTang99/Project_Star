# 云服务器访问与换机说明

更新日期：2026-10-23。用于 Project_Star 异步 PvP 开发。本文记录连接参数和公开指纹，不记录密码、私钥正文、数据库凭据或令牌。

## 1. 连接参数

| 项目 | 当前配置 |
|---|---|
| 建议 SSH 别名 | project-star-cloud |
| 公网地址 | 118.89.176.115 |
| SSH 用户名 | ubuntu |
| SSH 端口 | 22 |
| 认证方式 | 用户提供的 RSA 私钥；密钥名称 waynetang |
| 操作系统／架构 | Ubuntu 26.04 LTS，x86_64 |
| 用户公钥指纹 | SHA256:j3EwPo0CD8Bf2Z4I4dHRgVOZFG4qEDfd9UqYT3Hq+Mg（RSA 2048） |
| 服务器主机公钥指纹 | SHA256:VMBC3eEHcSXiPeXMRVWQriMIC3/M9yj3gMh0GL31laE（ED25519） |

主机指纹于上述日期通过已建立的 SSH 会话读取 /etc/ssh/ssh_host_ed25519_key.pub 核对。服务器重装或替换后主机指纹可能改变，须从云控制台核实再更新本文，不能直接绕过主机检查。

服务器地址或端口变化后同步修改本文；其他文档只链接本文，不复制维护连接参数。

## 2. 换电脑的必要准备

仓库只保存连接说明，不能代替私钥。换电脑后选择一种方式：

1. 通过自己的安全渠道迁移原 waynetang.pem 到新电脑的 ~/.ssh/waynetang.pem；或
2. 在新电脑生成密钥，并通过云控制台／已有授权连接把新公钥绑定到 ubuntu 用户。

私钥保存在仓库之外，不发送到聊天、不写进项目文档或发布包。采用新密钥时核实连接后更新本文的用户公钥指纹。仅复制仓库、没有可用私钥或其他认证方式时，AI 无法自动登录。

### 本机已有密钥位置

2026-10-22这台 Windows 电脑：用户原文件是 C:/Users/Admin25121001/Downloads/waynetang.pem；实际通过认证的受限权限副本为 C:/Users/Admin25121001/.ssh/project_star_waynetang.pem。两者对应上表同一个公钥指纹。副本用于解决下载目录继承权限过宽、OpenSSH 拒绝加载的问题，原文件未修改。

以上路径仅描述这台电脑；换机后 AI 应优先读取新机器现有 SSH 别名，或询问新路径，不能硬编码旧用户名与下载目录。

另外存在本轮生成但未上传服务器的 project_star_p0_ed25519 任务密钥，它不具备本服务器登录权限，不要误用。

## 3. 建议的本机 SSH 配置

将私钥保存到新电脑 ~/.ssh/waynetang.pem 后，可在 ~/.ssh/config 中添加（已有同名条目时编辑原条目，不重复追加）：

```sshconfig
Host project-star-cloud
    HostName 118.89.176.115
    User ubuntu
    Port 22
    IdentityFile ~/.ssh/waynetang.pem
    IdentitiesOnly yes
    StrictHostKeyChecking yes
```

私钥文件权限仅授予本人。Linux/macOS 通常使用 chmod 600；Windows 使用文件 ACL 限制访问，不能仅假定下载目录默认权限可用。若私钥有口令，在用户本机通过 SSH agent 解锁，不把口令发到聊天或写入命令。

首次连接前，使用云控制台等可信渠道比对上表主机指纹。ssh-keyscan 的结果本身不能证明主机身份；比对一致后才能登记到新机器 known_hosts。

配置完成后的只读检查：

```bash
ssh -o BatchMode=yes -o ConnectTimeout=10 project-star-cloud 'whoami; uname -m'
```

预期为 ubuntu 和 x86_64。也可以使用显式 -i <当前机器私钥路径>、-p 22 和 ubuntu@118.89.176.115，不要求一定创建 SSH 别名。

## 4. AI 接续执行顺序

1. 先读 AGENTS.md、当前实施计划、本文及服务器专项计划，确认本次任务范围。
2. 检查当前机器是否存在 project-star-cloud 别名或用户给出的密钥路径；不要读取或输出私钥正文。
3. 优先使用 BatchMode、连接超时和主机指纹检查连接；凭据缺失时只询问文件路径／别名，不要求用户粘贴密码或私钥。
4. 先做只读环境核实，再执行当前任务已授权的变更；以实际进程和文件为准，不假定历史部署状态仍然存在。
5. 构建可在本机执行；Linux 运行、依赖与容量验证在云端执行。用户已明确不继续使用本机 WSL 作为 Linux 测试目标。
6. 程序与玩家数据独立存放；构建、发布、回滚和旧程序清理不得覆盖数据库或存档。
7. 同步实际验收记录和阶段状态，不将源码基准、P0 测试账号或本地对手称作正式在线 PvP。

## 5. 云端目录与当前状态

- ~/project-star-p0/：隔离 P0 工具、SDK、源码及诊断工件。源码与官方专用模板导出已通过；不是玩家数据目录，也不是正式常驻业务服务。
- /opt/project-star/releases/p0-20261022-174039-133/：本轮通过的只读诊断程序；报告位于 /var/log/project-star/p0/。它运行检查后退出，没有常驻PvP端口。
- 正式目录建议：程序 /opt/project-star/releases/<version>/，玩家持久文件 /var/lib/project-star/，PostgreSQL 使用独立目录／卷，配置 /etc/project-star/，日志 /var/log/project-star/。创建与权限以执行记录为准，不因本文列出就认定已经部署。
- 账号及对局后端已部署：`project-star-api.service`，仅 `127.0.0.1:5080`；当前程序入口 `/opt/project-star/api-current`，PostgreSQL 位于 `/var/lib/postgresql/18/main/`，配置 `/etc/project-star/api.env`，持久文件 `/var/lib/project-star/`。账号、权威操作及真实快照PvP已通过API验证；Main联网面板经本机SSH隧道接入，使用见[联网客户端](ONLINE_CLIENT.md)。公网HTTPS、完整棋盘与回放仍待，详见[账号运行文档](ACCOUNT_SERVER.md)和[P4记录](../quality/ASYNC_PVP_P4_2026_10_23.md)。

后续位置、备份或服务名称变化在相应运行文档维护；不要将临时 P0 目录变成生产数据库目录。

2026-10-23继续P4后，当前入口已更新至`/opt/project-star/releases/p4-20261023-pvp-01/`，已接商店／构筑、选项事件、怪物战与真实快照PvP；两个正常构筑账号各完成两轮并互相消费快照。同一服务管理API、后台任务执行器与Godot规则子进程，数据库结构3，存档格式4。不能回滚到仅支持结构1或2的程序；旧规则存档和任务保留，但不跨版本自动恢复／执行。最新隔离源码`/home/ubuntu/project-star-tools/pvp-source/`，最终发布暂存`pvp-publish/`，通用account-publish已同步。最新范围见[权威存档](MATCH_PERSISTENCE.md)、[PvP契约](PVP_SERVER.md)及[P4证据](../quality/ASYNC_PVP_P4_2026_10_23.md)。

## 6. 连接问题排查

| 表现 | 先检查 |
|---|---|
| Connection timed out | 当前公网地址、云防火墙／安全组 TCP 22、本机网络 |
| Permission denied | 用户是否 ubuntu、密钥是否匹配、是否指定 IdentitiesOnly |
| UNPROTECTED PRIVATE KEY FILE | 私钥文件权限过宽，修正本人文件权限后重试 |
| Host key changed | 暂停连接，通过云控制台核实是否重装／替换；确认后更新指纹 |
| BatchMode 无法使用带口令私钥 | 用户先在本机解锁到 SSH agent |

本页不包含有效私钥，公开连接参数也不赋予登录能力。若项目将公开发布，请先审查是否保留具体服务器地址；私钥始终不得入库。
