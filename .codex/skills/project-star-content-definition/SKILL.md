---
name: project-star-content-definition
description: Query, add, update, or remove Project Star cards, skills, encounters, monsters, heroes, and card sets. Use for listing or filtering existing gameplay content, retrieving identity and level data, comparing definitions, and implementing content changes with dependency checks and focused verification. Excludes unrelated engine, UI, Git, and bug-fix work.
---

# Project Star 内容增删改查

以 scripts/Content/ 的 C# Definition 为唯一内容数据来源。根据用户意图查询、增加、修改或删除内容；具体玩法仍以项目设计文档为准。

## 选择操作

- **查询**：列出内容、按条件筛选、查看详情、比较等级或追踪引用。读取 [references/query.md](references/query.md)，返回当前源码中的数据与文件位置。
- **增加**：新增独立 Definition，组合通用机制，并验证注册与实际效果。
- **修改**：定位已有定义，精确调整请求的字段、等级或行为；同步相关描述与受影响的验证。
- **删除**：先查引用，处理已明确的依赖，再删除对应定义及其 .cs.uid；共享文件只移除指定类。

查询不隐含修改；增加、修改、删除不隐含 Git 提交或推送。用户已明确指定的内容操作直接执行，只有无法从上下文确定的玩法或依赖处理才需要澄清。

## 内容位置

| 内容 | 唯一数据来源 | 专项说明 |
|---|---|---|
| 卡牌、套装 | scripts/Content/Cards/、scripts/Content/Sets/ | [references/cards.md](references/cards.md) |
| 技能 | scripts/Content/Skills/ | [references/skills.md](references/skills.md) |
| 遭遇 | scripts/Content/Encounters/ | [references/encounters.md](references/encounters.md) |
| 怪物、英雄 | scripts/Content/Monsters/、scripts/Content/Heroes/ | [references/actors.md](references/actors.md) |

先读 AGENTS.md 与 docs/design/GLOSSARY.md，再按操作读取对应定义。只有内容变更才需读取匹配的专项说明、CONTENT_DATA.md 和相关玩法/架构规则；不要每次加载全部参考文件。

## 增加和修改

- 用户约定：新定义的卡牌默认同时生成并接入原画；仅在用户明确说明不需要时跳过。使用项目卡牌原画 Skill 和内置图像工具，按 Definition 尺寸生成并验证。
- 用展示名或完整 StringName key 定位；有同名或多个候选时先列出候选。重命名展示名与更改 key 是两个不同操作。
- 具体身份、等级数值、描述和能力组合只维护在 Definition。文档记录通用规则；规则变化时同步更新权威文档，不重新建立手工数据清单。
- 显示中文名，key 使用 card.、ability.、skill.、encounter.、monster.、hero.、set. 前缀。新术语/标签同步术语表和代码名称映射。
- 斜杠分级数值从初始等级开始到4级，具体约定见 CONTENT_DATA.md。数值存于等级配置，成长效果读取属性；同步修改同一行为的描述，保留未请求改变的等级。
- 复用通用 Ability / Effect / 对局结算定义；现有组合无法表达时才添加可复用机制，不在服务、模拟器或 UI 中按卡牌 key 分支。
- 新定义及 .cs.uid 放入对应 scripts/Content/ 目录。DefinitionRegistry.Scan 扫描公开、非抽象、无参构造的定义，不需要中央注册表手工登记。一个文件可能定义多个内容，文件数量不等于内容数量。
- 验证身份和等级配置，以及至少一个可观察的行为结果。行为检查位于 scripts/Presentation/Verification/，由 PhaseOneVerification.cs 注册；已有检查优先按改变的语义更新。
- 发现实际缺陷登记 BUG_LOG.md，新增内容本身的缺失不作为 BUG。

## 修改 key 和删除

1. 用 rg -n -F 搜索完整 key 和定义类名，检查运行引用、怪物卡组/技能、套装、遭遇奖励及验证；再查场景、资源和文档中的实际路径引用。
2. 区分真实内容依赖与通用分类筛选。删除一张卡不应删除通用属性、标签、能力或共用插画；技能和其他怪物可能仍使用它们。
3. 用户只要求删内容时，可以移除该内容专属的验证与注册项。若怪物卡组、固定奖励或其他仍保留的玩法依赖它，先列出实际依赖并确定替代/移除方式，不自动级联删除其他内容。
4. 按已确定的范围修改引用；更改 key 时同步所有实际引用。删除单独文件及对应 .cs.uid，共享文件保留其他定义。资源清理仅在请求范围内进行，先确认无消费者。
5. 通过注册校验和回归确认没有遗留引用；报告删除或修改的对象，以及受影响的内容。

## 验证与交付

只读查询无需构建或运行验证。内容变更依次运行：

1. dotnet build Project_Star.csproj
2. git diff --check
3. 可用的 .NET Godot 编辑器执行 --headless --path . res://Main.tscn -- --verify，实际路径与入口见 docs/quality/MANUAL_ACCEPTANCE.md。

报告具体内容变更、验证结果和实际环境限制。修改共享机制时保留固定战斗记录回归；仅当玩法明确改变对应基线时才同步基线，不用更新基线掩盖失败。不要因单次查询增加数据库、编辑器或持久化报表。
