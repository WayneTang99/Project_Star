# Release 自动导出

更新时间：2026-10-22。流水线：[release.yml](../../.github/workflows/release.yml)，导出脚本：[ExportRelease.sh](../../scripts/Infrastructure/Release/ExportRelease.sh)。

## 使用

1. 将流水线、导出脚本及其依赖的项目代码纳入要发布的提交，确保目标 tag 包含这些文件。
2. 在 GitHub 的 Releases 页面选择该 tag，发布 Release。正式发布与预发布均会触发；仅推送 tag 或保存 Release 草稿不会触发。
3. Actions 在 Ubuntu 24.04 runner 下载 Godot 官方 4.7.2 .NET Linux 编辑器和 .NET 模板，核对官方 SHA512 清单后构建。
4. 两种导出均成功后，自动上传两个附件到当前 Release。无需提供云端 SSH 密钥或额外 PAT；使用该次任务的 GITHUB_TOKEN 和 contents:write 权限。

附件名称：

- ProjectStar-linux-server-x86_64.tar.gz
- ProjectStar-windows-client-x86_64.zip

使用 GitHub CLI 上传到实际触发任务的 Release；重跑任务会替换同名附件，不会更新其他 Release。

## Linux 服务端的当前边界

用户已确认本次先导出 ServerBaseline.tscn。它是 P0 诊断程序，运行检查后退出，尚非正式常驻 PvP 服务；包内 README 明确标注。正式服务端实现后，更新 ExportRelease.sh 的入口与场景导出列表，以及对应启动冒烟测试。

服务端仅复制 scripts/ 与项目构建文件，不复制 art/、audio/、docs/ 或参考区；Godot 使用 dedicated_server 特性，并仅导出指定无界面场景。客户端以 Main.tscn 为唯一默认游戏入口，运行时代码位于 scripts/Presentation/Game/，原试玩入口已迁入Main；独立验证页为Verification.tscn，组件展示位于Presentation/Verification/。客户端暂存复制这两个根场景与运行所需美术、音频，发布工件不写回代码分支。

服务端包导出后会在 runner 内实际运行一次，诊断报告写到程序目录之外；失败时不会上传附件。Windows 包在 Linux 上交叉导出，不在 Linux runner 上进行图形试玩。

## 产物与代码、玩家数据分开

所有下载、隔离源码、导入缓存、导出目录和压缩包均位于 RUNNER_TEMP，不生成到 Git 检出目录中。流水线没有 git add、commit 或 push，不建立产物分支，也不把发布包写入源码分支。仓库只维护 yml、脚本与说明。

Linux tar 包保留可执行权限；Windows zip 包含 exe、pck 和完整 data_* 依赖目录，下载后整体解压，不能只取 exe。包不包含数据库、玩家存档、私钥或服务器配置。

此流水线负责发布下载附件，不自动部署云服务器，也不执行数据库迁移。下载后的部署仍须将程序与玩家数据分开；更换程序不能覆盖存档。

2026-10-23新增的.NET10账号宿主独立于Godot游戏工程，目前采用显式云端部署，尚未加入本流水线；见[账号服务](ACCOUNT_SERVER.md)。现有setup-dotnet 8仍只负责Godot项目，不把P0附件标作已包含账号API的正式服务端。

## 环境来源与维护

本方案按用户确认使用 GitHub 的 Ubuntu runner 和 Godot 官方下载文件。Godot 官方 build-containers 用于编译引擎，不把第三方 godot-ci 镜像标作官方镜像。

- Godot 版本在 release.yml 的 GODOT_VERSION 中维护，必须与 Project_Star.csproj 的 Godot.NET.Sdk 版本一致，流水线会检查。
- 当前 C# 项目为 net8.0，使用 .NET 8 SDK。框架升级时同步更新 setup-dotnet。
- actions/checkout 和 actions/setup-dotnet 固定到已核实的提交，升级时核对来源。
- export_presets.cfg 在临时项目中生成，不需要提交本机导出配置。
- 客户端图标源位于 `art/branding/project-star-icon.png`，Windows ICO 包含16、32、48、64、128、256像素尺寸；项目窗口使用该ICO，客户端导出开启 `application/modify_resources` 并显式指定同一ICO，写入EXE文件图标。服务端隔离源码移除两项图标引用，不复制美术资源。
- 如果仓库启用不可变 Release 等限制、组织禁用写权限，附件上传可能被 GitHub 拒绝；按仓库发布策略调整，不将包改为提交到分支。

## 验证记录

2026-10-23：新图标在本机官方4.7.2.NET编辑器实际导出Windows客户端后，通过Windows资源接口读取EXE的图标组及图像，16／32／48／64／128／256像素全部逐字节匹配正式ICO；PCK包含项目PNG及原生ICO。C#构建0警告0错误，发布脚本Bash语法通过。产物位于本机 `output/branding/windows-client/`；本次未触发线上Release。

2026-10-22：YAML 解析、actionlint、Bash 语法及无分支写入检查通过；同一导出脚本的 Linux server 分支在云端使用官方 Linux 编辑器／模板实际构建并运行通过。Windows 客户端另用相同导出选项在本机官方编辑器验证。实际 GitHub Release 事件及附件上传尚未触发，需首次正式发布时确认。

参考：[GitHub Release 触发事件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release)、[GitHub CLI 附件上传](https://cli.github.com/manual/gh_release_upload)、[Godot 专用服务器导出](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_dedicated_servers.html)、[Godot 官方构建容器](https://github.com/godotengine/build-containers)。
