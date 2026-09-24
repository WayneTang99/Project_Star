# BUG_LOG

本文档记录项目每次 BUG 的发现与修复记录。发现即登记，修复后补全根因与修复方案。

## 登记约定

- 编号：`BUG-001` 格式，零填充，单调递增，不复用。
- 状态：仅限 `Open` / `Fixing` / `Fixed` / `Won't Fix` / `Duplicate`。
- 位置：`文件路径:行号` 或 `模块/类名`，保证可定位。
- 根因与修复方案：发现时可留空；状态改为 `Fixed` 时必须填写。
- 发现即登记，不等修复。

## 记录表

| 编号 | 发现日期 | 位置 | 问题描述 | 根因 | 修复方案 | 状态 |
|---|---|---|---|---|---|---|
| BUG-001 | 2026-09-19 | `Project_Star.Tests/Application/Common/ResultTests.cs:9` | 普通 xUnit 测试进程未初始化 Godot 运行时，构造 `StringName` 时触发 `AccessViolationException` 并中止整个测试运行。 | 错误采用了独立 xUnit 测试宿主；该宿主不具备 Godot 原生运行时上下文。 | 移除 xUnit 测试项目；后续验证统一通过 Godot 内手动测试入口执行。 | Fixed |
| BUG-002 | 2026-09-19 | `project.godot` / 第 1 步交付 | 清理 xUnit 后未提供 Godot 主场景，用户无法执行约定的手动验证。 | 测试方式调整时只移除了错误入口，没有同步补齐替代入口。 | 新增 `Main.tscn` 和第 1 步验证界面，并将其配置为主场景。 | Fixed |
| BUG-003 | 2026-09-19 | `Playtest.tscn` / 第 8 步交付 | 遭遇排程实现后只加入规则验证，试玩主界面仍停留在最小战斗流程。 | 实现时遗漏了将新应用用例接入试玩入口。 | 在试玩场景加入遭遇生成、三选一、第 4 回合怪物战、第 8 回合 PvP 与回合推进。 | Fixed |
| BUG-004 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` | 试玩仍要求玩家按开发测试步骤手动创建、购买、放置、战斗和生成遭遇，不符合正常对局流程。 | 试玩入口按功能验证顺序组织，而非按游戏状态流组织。 | 重写试玩控制器：选英雄后自动进入遭遇，并按商店、事件、战斗准备、结算和下一回合切换界面。 | Fixed |
| BUG-005 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 商店 | 同一商店报价购买后仍可重复购买。 | 试玩商店未保存本次报价的售罄状态。 | 购买成功后标记报价售罄并隐藏购买按钮。 | Fixed |
| BUG-006 | 2026-09-19 | `Playtest.tscn` / 战斗准备 | 试玩没有提供可操作的双棋盘，只能自动把下一张卡放入战场。 | 第 5 步棋盘服务未接入正常对局 UI。 | 增加战场 10 格和备战区 10 格的点击操作。 | Fixed |
| BUG-007 | 2026-09-19 | `scripts/Application/Encounters/EncounterScheduler.cs` | 普通回合可能抽到怪物或 PvP 遭遇。 | 普通候选池未按遭遇类型排除特殊遭遇。 | 普通回合只从 Shop 和 Other 中抽取；怪物/PvP 仅走第 4/8 回合分支。 | Fixed |
| BUG-008 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 双棋盘 | 双棋盘只在战斗准备界面显示，其他对局阶段无法调整。 | 表现层错误地把棋盘可见性绑定到战斗准备状态。 | 棋盘改为对局内常驻区域；遭遇、商店、事件、准备和结算阶段均可操作。 | Fixed |
| BUG-009 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 商店购买 | 购买的卡牌进入了不存在于玩法设定中的库存，必须再次手动放置。 | 试玩界面错误地把底层卡牌归属集合实现成了第三个放置区域。 | 移除库存界面与手动放置入口；购买前检查双棋盘空间，成功后优先自动放入战场区，其次放入备战区。 | Fixed |
| BUG-010 | 2026-09-22 | `scripts/Presentation/PhaseOneVerification.cs` / 内容拆分 | 删除旧测试内容定义后，验证入口、试玩入口和本地测试对手仍硬编码引用旧类型与 key，导致构建失败或运行时查找失败。 | 验证和试玩错误地依赖了占位内容，而非各自使用验证数据或通用注册表选择。 | 验证入口改用内部专用定义；试玩和本地对手按稳定 key 顺序选择注册表中的正式内容，并在内容为空时提供明确状态。 | Fixed |
| BUG-011 | 2026-09-22 | `HeroDefinition` / `HeroInstance` | 英雄模型错误持有卡牌分类用的 `TagSet`，导致玩家角色与卡牌标签概念混淆。 | 早期通用定义模型将英雄和卡牌都接入了同一标签扩展点，未落实标签仅用于卡牌分类的规则。 | 从英雄定义、英雄实例和实体工厂移除标签；保留 `Human` / `Mechanical` 作为卡牌标签，并同步设计与架构文档。 | Fixed |
| BUG-012 | 2026-09-22 | `scripts/Domain/Combat/BattleRuntime.cs` / 零能力卡牌 | 正式卡牌没有配置能力时，战斗层会自动补充旧版普通攻击，导致“没有功能”的卡牌仍能造成伤害。 | 早期战斗测试使用空能力列表触发兼容攻击，但该兼容路径未与正式卡牌快照区分。 | `CardBattleSetup` 增加显式兼容标记；正式卡牌快照关闭旧版攻击，仅底层旧测试输入默认保留兼容行为。 | Fixed |
| BUG-013 | 2026-09-22 | `scripts/Content/Cards/JewelryBagCardDefinition.cs` | 珠宝袋被错误定义为只支持2级，导致实例无法升级，也永远无法在4级出售时随机获得4级钻石。 | 将“2级卡牌”误解为“仅存在2级”，没有区分初始等级与支持等级。 | 保持初始等级2，补充3级和4级配置；出售奖励继续按实例当前等级筛选材料，并增加4级钻石候选验证。 | Fixed |
| BUG-014 | 2026-09-22 | `scripts/Presentation/PhaseOneVerification.cs` / `scripts/Application/Combat/BattleSetupFactory.cs` | 内容拆分后第1～9步验证中，珠宝袋奖励与战斗快照相关验证失败。 | 验证专用战斗卡在替换旧内容时遗漏攻击属性和能力；无能力正式卡仍被旧冷却校验拒绝；棋盘满载用例只占满战场而遗漏备战区。 | 补全验证卡的通用攻击能力；允许关闭旧版攻击的零能力卡使用零冷却；满载用例同时占满战场与备战区。 | Fixed |
| BUG-015 | 2026-09-22 | `scripts/Presentation/MinimalPlaytest.cs` / 怪物战 | 野猪的零攻击卡组看起来仍会攻击玩家，且战斗准备界面看不到怪物卡牌。 | 日蚀伤害日志未标注伤害来源；试玩界面只渲染玩家棋盘，未渲染已创建的敌方战场。 | 为伤害事件记录卡牌、状态和日蚀来源并在日志中显示；增加只读敌方战场区，在战斗准备与结算阶段展示对手卡牌。 | Fixed |
| BUG-016 | 2026-09-22 | `scripts/Content/Cards/LightCavalryCardDefinition.cs` | 轻骑兵发动时把攻击伤害施加给己方英雄；军靴提供疾速后会缩短错误能力的等待时间。 | 轻骑兵把伤害与己方卡牌强化组合在同一个目标为 `AlliedHero` 的能力中，伤害效果沿用能力目标；验证只检查伤害数值，未检查目标阵营。 | 将轻骑兵主动能力目标改为 `EnemyHero`；己方人类卡牌强化继续按能力来源阵营结算，并补充轻骑兵伤害目标验证。 | Fixed |
| BUG-017 | 2026-09-23 | `scripts/Presentation/PhaseOneVerification.cs:1266` / `:1796` | 第 1～9 步验证中，臂铠和教团远征军两项错误地显示失败。 | 元素属性断言与正式卡牌定义及 `docs/design/CARD_DATA.md` 相反：臂铠被要求为 Light，教团远征军被要求为 General，导致战斗效果检查前直接返回失败。 | 将臂铠断言改为 General、教团远征军断言改为 Light。 | Fixed |
| BUG-018 | 2026-09-23 | `docs/engineering/ARCHITECTURE.md` / `docs/planning/IMPLEMENTATION_PLAN.md` | 文档称被动光环进入 `PendingAbility` 队列，并将已由现有模型承担的职责列为独立类型，导致第 7 步状态被误判。 | 实现采用主动/回响入队、被动光环按当前状态求值；文档没有随实现边界更新。 | 同步架构与计划描述，明确现有来源检查、魔法检查和回响截断的实现位置。 | Fixed |
| BUG-019 | 2026-09-23 | `scripts/Presentation/MinimalPlaytest.cs` / 选角 | 将试玩创建参数设为 999 后，首轮收入立即发放，玩家实际起始显示 1004 金，不符合测试目标。 | `CreateMatchService.Create` 会在创建对局时结算第 1 轮收入，试玩把目标余额误当成结算前余额。 | 试玩创建对局时从目标余额 999 中扣除英雄初始收入作为创建参数，首轮收入结算后显示 999。 | Fixed |
| BUG-020 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckMonsterRewardRetention` | Godot 规则验证的“棋盘已满时怪物卡牌奖励保持待领取”用例抛出未知阵营异常。 | 测试使用帕拉帝恩臂铠占位，但专用定义注册表未包含其阵营。 | 改用已有的无阵营兽皮占位，并复用同一兽皮定义注册到测试用定义表。 | Fixed |
| BUG-021 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckCardSetBonuses` | 套装战斗快照验证因未知套装异常失败。 | 测试创建 `BattleSetupFactory` 时漏传已注册的套装定义。 | 将同一注册表的套装定义传给战斗快照工厂；无界面规则验证 90 项全部通过。 | Fixed |
