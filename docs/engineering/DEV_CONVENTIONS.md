# DEV_CONVENTIONS.md
Project_Star 开发约定。供 AI 代理 / 开发者提交代码、写注释时参考；与 `AGENTS.md`（导航）、`docs/engineering/ARCHITECTURE.md`（架构）配合使用。

## Git 工作流

- **main**：主分支（生产环境）
- **dev**：开发分支（日常开发）
- **feature/功能名称**：功能分支
- **hotfix/问题描述**：修复分支
- **默认推送 dev**：日常提交/推送仅操作 `dev` 分支，**禁止推送 main**（除非用户明确要求）。
- **默认不加代理**：拉取/推送默认直连（git 不配置 http/https proxy）。
- **拉取失败用代理重试**：直连失败时，临时用代理 `http://127.0.0.1:7897` 重试（仅本次命令生效，不写入配置）：
  `git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 pull origin dev`

## 提交规范

格式：`<type>(<scope>): <subject>`

- `feat: 添加玩家冲刺功能`
- `fix: 修复碰撞检测问题`
- `docs: 更新 API 文档`

## 代码注释约定

- **类前注释（必填）**：每个类 / 抽象类 / 接口定义前写普通 `//` 短注释，说明该类职责与所属层级/模块（如：`// 对局资源容器（对局层）`）。
- **关键外部调用函数注释（必填）**：被外部模块调用的 public 方法（如应用用例、聚合根操作入口、跨层适配接口）写普通 `//` 短注释，说明用途；纯内部逻辑方法（private / 仅本类使用）可不写。
- **注释语言**：一律使用中文，术语引用 `docs/design/GLOSSARY.md` 中的英文名（如 `StringName` / `ApplyModifier`）。
- **格式**：保持简洁，单行 `//` 注释为主，不展开长段说明。

## 工具与产物约定

项目硬约束见 [AGENTS](../../AGENTS.md)，不在此复制。

- 不直接修改 `.godot/`；导入/构建由工具管理缓存。
- .NET SDK ≥ 8.0；bin/、obj/ 和 output/ 不提交。
- output/ 保存日志、截图临时输出与生成草稿，并用 .gdignore 隔离导入；验收所需代表截图位于 docs/quality/。
- 正式内容 CSV 使用 keep 导入模式，保留原文件供 FileAccess 读取，不维护自动生成的翻译产物。
- 默认不提交 export_presets.cfg；需要版本管理导出配置时按任务范围决定。
