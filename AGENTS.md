# AGENTS.md

Project_Star 项目导航。本文件为 AI 代理 / 开发者的入口文档，提供项目概览与文档导航；具体规则分散在各专项文档中。

## 项目概览

- 基于 **Godot 4.7** 的 3D 游戏项目，脚本使用 **C# / .NET**
- 物理引擎：**Jolt Physics**
- 类型：类《The Bazaar》（大巴扎）的**卡牌异步对战自走棋**

## 开发工作约定

- **先想再写，不懂就问**：写代码前先陈述你的假设、歧义和权衡。如果需求不明，必须停下来提问，而不是自己猜一个。
- **做减法，极简优先**：只写解决当前问题的最小代码。不要添加没要求的抽象、配置、兼容性或"以后可能用到"的功能。
- **精准修改，不碰无关代码**：只改必须改的地方。不要"顺手"优化旁边的格式或重构无关逻辑。改动范围要可控，方便审查。
- **目标驱动，可验证**：把模糊任务（如"修 bug"）变成可验证的目标（如"写一个能复现该 bug 的测试，然后修到测试通过"）。
- **BUG 必登记**：发现 bug 一律登记到 `docs/quality/BUG_LOG.md`（编号/日期/位置/描述/根因/修复方案/状态），修复后补全根因与修复方案。

## 硬约束

每次写代码都必须遵守：

- **代码统一放入 `scripts/`**：所有 C# 源文件及对应 `.cs.uid` 必须位于项目根目录的 `scripts/` 下，并按 `Domain`、`Application`、`Infrastructure`、`Presentation`、`Content` 分层；项目根目录不得再建立并列的代码目录。
- **Key 统一用 `StringName`**：所有标识性 key 一律用 `Godot.StringName`，展示文本保持 `string`。
- **身份字段放属性集**：身份字段（key / 展示名 / 归属 / 尺寸 / 元素属性）放进实体属性集的只读身份分区，不放实体 Node 上。
- **能力必须可复用**：能力是通用动作单元，一张卡由多个已有能力组合而成。禁止为单张卡写专属能力类（如 `FrostSwordAbility`）。新能力只有当现有能力组合无法表达所需逻辑时才新建，且必须能被多张卡复用。
- **效果堆叠约定**：Apply 累加（施加必须累加到目标属性，而非覆盖赋值）；到期扣减（扣减该实例贡献的量，而非直接清零）；不清零（移除不再直接置 0，属性由衰减逻辑自然归零）。
- **数据/视图分离**：棋盘等数据层保持纯数据，UI 通过应用用例提交 Command，并读取 Snapshot / ViewModel 刷新，不直接修改模型。
- **确定性战斗**：数值计算一次完成、结果确定可回放，禁止依赖其他卡牌最终值。

### 禁区列表

- 禁止在战斗层之外操作战斗状态。
- 禁止绕过应用用例跨层修改状态；Command / Result / Domain Event 按架构约定分工。
- 禁止 UI 直接修改模型数据。
- 禁止在能力中硬编码卡牌专属逻辑。
- **`ref/` 为参考区**：仅用于设计时人工查看参考图和参考文档；允许新增、修改、删除、移动和重命名其中的文档文件，其他类型的内容保持只读。
- **禁止项目引用 `ref/`**：代码、场景、资源、配置、文档链接及构建脚本均不得把 `ref/` 内文件作为项目依赖或运行时资源。
- **`ref/` 不参与构建与打包**：不得将其加入 `.csproj`、`project.godot`、Godot 导入链、导出配置或发布产物；也不得为了规避导入而在 `ref/` 内生成辅助文件。

## 文档导航

| 文档 | 权威内容 | 何时查阅 |
|---|---|---|
| docs/worldbuilding/README.md | 世界观入口、专题职责与维护规则 | 补充剧情、人物背景与世界设定 |
| docs/design/GAME_DESIGN.md | 玩法规则 | 实现游戏功能 |
| docs/design/GLOSSARY.md | 英中术语与定义 | 提出需求、命名和写代码前 |
| docs/design/CONTENT_DATA.md + scripts/Content/ | 内容字段与维护约定 / 正式定义 | 新增、修改和核对内容 |
| docs/design/UI_SYSTEM.md | 现行组件、页面、交互与卡面规范 | 修改界面、棋盘和卡面 |
| docs/engineering/ARCHITECTURE.md | 分层、所有权、事务、战斗与扩展入口 | 编码与架构审查 |
| docs/engineering/DEV_CONVENTIONS.md | Git、注释、工具和产物约定 | 提交与工具操作 |
| docs/planning/IMPLEMENTATION_PLAN.md | 当前状态与未完成队列 | 确认下一步 |
| docs/quality/MANUAL_ACCEPTANCE.md | 验证入口、当前证据与人工矩阵 | 验证试玩 |
| docs/quality/BUG_LOG.md | BUG 编号、根因、修复和状态 | 发现和修复 BUG |

正式内容以 scripts/Content/ 下的 C# Definition 为唯一数据来源，按 Cards、Skills、Heroes、Encounters、Monsters、Mentors 和 Sets 分类。Markdown 解释规则，不重复维护具体内容数据。现行规则只在上述权威位置维护，已完成计划的过程记录由 Git 保存。
