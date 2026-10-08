# BUG_LOG

本文档记录项目每次 BUG 的发现与修复记录。发现即登记，修复后补全根因与修复方案。

历史位置与行号按发现时版本记录；目录整理后的现行模块见架构文档，原文件和验证入口可在 Git 历史定位。

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
| BUG-022 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckChargeSkill` | 冲撞护甲验证错误地显示失败。 | 用例中的触发卡牌先造成 1 点伤害，已消耗敌方 1 点护甲，却仍断言冲撞吸收完整 5 点护甲。 | 将触发卡牌伤害设为 0，仅保留发动事件；无界面规则验证 93 项全部通过。 | Fixed |
| BUG-023 | 2026-09-24 | `scripts/Content/Skills/ChargeSkillDefinition.cs` / 冲撞伤害 | 冲撞被定义为按技能等级预先计算的固定伤害，不符合“自身等级”指英雄等级的规则。 | 将描述中的自身等级误解为技能等级，且战斗快照没有提供英雄等级。 | 战斗快照加入英雄等级，改用按来源方英雄等级乘技能等级系数的通用伤害效果，并以不同英雄等级验证。 | Fixed |
| BUG-024 | 2026-09-24 | `scripts/Content/Cards/HolySlashingBladeCardDefinition.cs` / 黎明之剑 | 黎明之剑的主动效果会随机摧毁敌方中型恶魔或亡灵卡牌。 | 卡牌能力配置与“仅小型”的目标规则不一致，验证用例也将中型当成可选目标。 | 将摧毁尺寸限制为小型，同步正式卡牌描述，并调整战斗验证确保中型卡牌存活。 | Fixed |
| BUG-025 | 2026-09-24 | `scripts/Infrastructure/Definitions/DefinitionRegistry.cs` / `ValidateEncounter` | 启动试玩扫描遭遇定义时，酒馆交易因无效效果异常中止。 | 校验器没有识别新增的跨英雄购买、下场战斗最大生命加成和战场卡牌属性修改效果，未知效果默认拒绝。 | 增加这三种通用遭遇效果的参数校验，并在规则验证中检查酒馆与校场定义可以通过注册。 | Fixed |
| BUG-026 | 2026-09-24 | `scripts/Domain/Combat/CombatSimulator.cs`、`scripts/Application/Encounters/ResolveEncounterOptionService.cs` / 荆棘甲攻击强化 | 校场漏掉荆棘甲；铁匠铺虽然写入其攻击属性，战斗伤害却不受强化影响。 | 将“攻击能力”错误地等同于基础攻击值大于0；荆棘甲伤害只读取己方英雄护甲，战斗状态也未登记其攻击属性为可强化属性。 | 荆棘甲伤害增加来源攻击属性；该属性登记为能力支持属性；校场识别伤害效果引用该属性的卡牌。铁匠铺和校场均验证荆棘甲攻击强化。 | Fixed |
| BUG-027 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / 卡面组件类型 | 接入 `CardFace` 后 C# 构建失败，编译器将 `CardFace` 解析为命名空间而非类型。 | `CardFace` 命名空间与其中的同名类型冲突，裸类型名引用产生歧义。 | 为控件类型添加 `CardFaceControl` 别名；`dotnet build Project_Star.csproj --no-restore` 成功，0 警告、0 错误。 | Fixed |
| BUG-028 | 2026-09-30 | `scripts/Presentation/Playtest/BoardZoneView.cs` / `CardItemView.cs` / `CardFace.cs` | 固定17%卡面导致文字约4px，中大型卡牌只占起始按钮，缩放不重排。 | 逐格按钮承载整张卡牌，整体缩放同时压缩字体，布局与数据更新耦合。 | 独立实体视图覆盖完整占格，棋盘统一格标尺；卡面按实际尺寸布置子模块并保留13px文字下限，装饰独立缩放；Resize仅重排。108项回归及原生拖拽输入通过，1280×720/1920×1080紧凑棋盘实际图已核对；其他窗口视觉仍待P6。 | Fixed |
| BUG-029 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `CreateCardFaceViewModel` | 已有臂铠、野猪、审判之锤原画无法匹配。 | 映射使用 `armguard` 等字符串，而正式身份 key 为 `card.armguard` 等完整值。 | CardDisplayAdapter 改为完整 StringName key 映射、缓存纹理并提供统一缺图占位；Godot 4.7.2 headless 检查三张原画路径及占位复用通过。 | Fixed |
| BUG-030 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `CreateCardFaceViewModel` | 卡面漏显示修女治疗、审判之锤百分比伤害，并忽略实例的对局属性加成。 | 仅枚举等级定义的四个基础属性，没有读取能力效果语义和实例当前只读数值。 | MatchSnapshot 复制实例基础/当前属性及能力，CardDisplayAdapter 共用卡面与详情格式化，治疗和百分比保留语义；Godot 验证治疗10、最大生命20%及校场加成通过。 | Fixed |
| BUG-031 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `ResolveEventOption` | 普通事件获得卡牌或修改卡牌属性后，当前棋盘不能立即反映变化。 | 成功分支仅更新文字和资源栏，遗漏棋盘刷新。 | 所有操作由 MatchPresenter 执行后统一 RefreshView，MinimalPlaytest.Render 同步刷新 HUD、棋盘和详情；真实 Playtest 场景按钮驱动的垃圾场获得与校场强化检查通过。 | Fixed |
| BUG-032 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `ChooseEncounter`、`ShowPreparation`、`HideAllActions` | 进入怪物战或 PvP 准备时，敌方棋盘数据已生成，面板仍可能保持隐藏。 | `HideAllActions` 在创建敌方前按 `_enemy == null` 隐藏面板，`ShowPreparation` 创建敌方后仅刷新内容，没有更新面板可见性。 | 移除 HideAllActions；MatchPageViewModel 同时给出敌方快照与 EnemyVisible，由 Render 设置面板可见性；真实 Playtest 场景的怪物/PvP 准备检查通过。 | Fixed |

| BUG-033 | 2026-09-30 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / RefreshAndStaleOffers | 新增旧报价回归检查失败。 | 测试误用仅三件可售商品的小型商店，按规则不可刷新，却假定刷新成功后报价批次已改变。 | 改用有四件可售商品的中型商店，并显式确认报价批次已变化后再验证旧商品操作。 | Fixed |
| BUG-034 | 2026-09-30 | `scripts/Application/Match/MatchSnapshot.cs` / CardSnapshot.Value | 卡牌快照显示基础价值，出售用例按含 Modifier 的最终价值结算，存在显示与回补不一致。 | 快照使用 GetBaseValue(Value)，经济用例使用 GetFinalValue(Value)。 | 快照改为读取最终价值；Godot 验证加成前后快照隔离及实际出售金额与显示值一致。 | Fixed |

| BUG-035 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / RenderedScene | P2 场景迁移后，真实场景验证因找不到旧节点或转换为 GridContainer 失败。 | 回归检查硬编码了旧场景路径与逐格按钮类型。 | 改为读取 MatchShell 的动作和独立 CardItemView；补验节点复用、共列布局及详情限位；104项通过。 | Fixed |
| BUG-036 | 2026-10-01 | `scripts/Presentation/Playtest/MatchShell.cs` / 商品显示 | 初次迁移商品操作时只显示购买价格，遗漏卡名与卡面概要。 | 直接使用 UiAction.Text，未组合只读 CardSnapshot 的显示字段。 | 恢复共用 FormatCardFace 与完整详情 tooltip；购买事件继续携带同次报价版本。 | Fixed |
| BUG-037 | 2026-10-01 | `scripts/Presentation/CardFace/CardFace.cs` / Title | 组件展示的长名称越过卡面边界，覆盖邻近区域。 | Title 没有启用裁切和超长文本省略。 | 启用 ClipText 与 TrimEllipsis；完整名称保留在 tooltip/详情，1280×720与1920×1080实际渲染图确认长名不越界。 | Fixed |
| BUG-038 | 2026-10-01 | `scripts/Presentation/Playtest/MatchShell.cs` / LeavePanel | 新增展示入口后，多项右侧操作同时可见时，VBox 最小高度可能超过 ContextRow。 | 直接给 VBox 设尺寸不能压缩子按钮累计最小高度。 | LeavePanel 放入固定行高的 LeaveScroll，横向禁滚、纵向按需滚动；场景回归105项通过。 | Fixed |
| BUG-039 | 2026-10-01 | `scripts/Presentation/PhaseOneVerification.cs` / 原生输入验证 | 延迟执行 async lambda 时出现 Task 不能转换为 Variant 的运行错误。 | Callable.From 选中了返回结果的泛型重载。 | 显式转换为 Action 使用 async void 回调，并捕获验证异常以退出码报告失败；原生验证通过。 | Fixed |
| BUG-040 | 2026-10-01 | `scripts/Presentation/Playtest/CardItemView.cs` / SelectionOutline | 展示页没有调用 SetSelected 时，所有卡面被默认面板背景压暗。 | 新建轮廓 Panel 默认可见，尚未替换默认样式。 | 轮廓初始隐藏，仅选中或焦点时显示透明底边框；实际图确认卡面亮度正常。 | Fixed |
| BUG-041 | 2026-10-01 | `scripts/Presentation/Playtest/CardItemView.cs` / _GetDragData | 原生输入拖拽不能开始。 | Godot 传入按下位置，拿该位置与按下位置计算阈值永远得到0。 | 原生 _GetDragData 直接生成身份载荷；显式8px阈值由 MouseMotion 检查并 ForceDrag，避免重复启动；原生输入验证通过。 | Fixed |
| BUG-042 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / NativeDrag | Headless主窗口合成鼠标输入时，拖放预览读取到(0,0)，无法提交。 | 测试把主窗口系统鼠标位置与合成事件位置混用，并遗漏鼠标进入通知。 | 使用独立SubViewport、NotifyMouseEntered和本地PushInput驱动原生GUI；Escape同步输入动作状态；阈值/跨区/释放一次/取消均通过。 | Fixed |
| BUG-043 | 2026-10-01 | `scripts/Presentation/CardFace/CardFace.cs` / LayoutCompact | 小型紧凑卡面两侧角饰重叠，遮住名称与数值。 | NinePatch角饰仍保留原生40px边缘，而卡面宽度可能不足80px。 | 角饰保留原生采样尺寸单独缩放，文字与价值提高绘制层级；双分辨率实际图确认无覆盖。 | Fixed |
| BUG-044 | 2026-10-01 | `scripts/Presentation/CardFace/CardValueBadge.cs` / 紧凑价值 | 小型卡牌的持有价值贴着右侧边框，单个数字难以辨认。 | 价值文字右对齐到紧凑卡面的角饰区域。 | 紧凑状态改为紧邻金币图标左对齐，文字13px以上；实际图中1/2/12持有价值均可辨认。 | Fixed |
| BUG-045 | 2026-10-01 | `scripts/Presentation/Playtest/MatchPresenter.cs` / BuyCard | 购买与放置分步编排，合并到尚未放置的库存目标时，该目标仍游离在棋盘之外。 | 界面只对WasCreated结果放置，合并预检也未考虑消耗卡牌释放的空间。 | 应用层BuyAndPlace/AcquireAndPlace统一事务，预检纳入消耗卡牌释放位置；连续合并后目标放置及满盘失败保留检查通过。 | Fixed |
| BUG-046 | 2026-10-01 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / _ExitTree | P4截图模式退出时报告不存在的Resized连接。 | 截图分支提前返回，未建立普通展示模式的Resized订阅，退出仍无条件解除。 | 仅普通展示模式解除对应连接；双分辨率截图进程不再报告断连错误。 | Fixed |
| BUG-047 | 2026-10-01 | `scripts/Presentation/Playtest/CardDetailsView.cs` / HeroDetailsView | 出售和奖励浮层背景过于透明，文字与底下棋盘/商品混在一起。 | 默认主题Panel带透明背景，缺少不透明浮层底色。 | 两个浮层设置不透明底色、边框与内边距，双分辨率实际渲染图确认文字独立可读。 | Fixed |
| BUG-048 | 2026-10-01 | `scripts/Presentation/Playtest/ShopView.cs` / LayoutView | 第一版商品卡面高于上排滚动区域，初始位置看不到卡面底部价值。 | 商品使用固定140px高度，没有按内容行可用高度重排。 | 商店卡面高度随滚动区域变化，保持三种尺寸比例；1280×720/1920×1080截图确认完整卡面可见。 | Fixed |
| BUG-049 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / TransactionScene | 新增真实出售按钮验证找不到VBoxContainer/Sell节点。 | 测试假定动态节点使用类型名，Godot实际生成带序号的名称。 | 浮层内容容器显式命名Content，验证使用稳定路径；114项回归通过。 | Fixed |
| BUG-050 | 2026-10-01 | `scripts/Presentation/Playtest/HeroDetailsView.cs` / ClaimRequested | 对局结束后奖励列表误禁用领取，与原奖励用例及右上领取入口不一致。 | 用棋盘移动权限BoardEnabled代替奖励领取权限，并在新协调入口追加终局禁令。 | 奖励按钮读取Reward.Enabled，领取遵循原应用用例；棋盘仍只读，移除额外终局限制。 | Fixed |
| BUG-051 | 2026-10-01 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / CaptureCardPool | 全卡池截图的右列与下排超出画面，无法看到全部17张卡牌。 | 截图网格使用1920×1080布局，但窗口仍按项目默认逻辑尺寸拉伸，网格被再次放大。 | 仅全卡池截图模式将窗口逻辑尺寸设为1920×1080，等待布局刷新后再创建网格和截取；重新核对全部17张卡面。 | Fixed |
| BUG-052 | 2026-10-01 | `docs/design/SkillDataTable.csv.import` / Godot 编辑器资源扫描 | 导入原画时编辑器报告技能 CSV 与生成的名称翻译资源 UID 重复。 | 正式内容 CSV 错用翻译导入器，将业务字段当作语言列生成翻译资源，其中技能名称翻译资源与源表共用 UID。 | 4份正式 CSV 改用保留原文件的 keep 导入模式，清理35个没有消费者的翻译产物；编辑器重新导入无UID重复且不再生成翻译资源，CSV原文与参考区哈希不变，构建0警告0错误，Godot115/115验证通过。 | Fixed |
| BUG-053 | 2026-10-01 | `scripts/Presentation/Playtest/BattlePlaybackPresenter.cs` / `ComponentShowcase.cs` | P5 初次构建的播放游标及截图夹具类型引用不匹配。 | Tick 使用 long、秒数使用 decimal，新入口误用 int 和不正确的枚举命名空间。 | 游标统一 long，播放时钟显式转换 decimal 为 double；修正 SideId 与 CardAcquisitionSource 的命名空间。构建0警告0错误。 | Fixed |
| BUG-054 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` | 新增播放页后两项旧场景回归失败。 | 旧流程假定战斗按钮立即进入结果，页面互斥夹具未给播放页敌方数据。 | 真实场景检查先验证播放页与暂停/倍速按钮，再跳过；更新枚举页夹具，补验冻结结算不被重复操作修改。117/117通过。 | Fixed |
| BUG-055 | 2026-10-01 | `scripts/Presentation/Playtest/LeavePanel.cs` / `CardItemView.cs` | P5 首次截图中跳过按钮需向下滚动，冷却状态文字靠近效果数值。 | 开发入口排在播放控制之前，状态浮层按底部固定偏移放置。 | 播放控制置前并隐藏开发入口；状态放到插画区域，完整并存状态加入提示；1280×720和1920×1080重新截图确认。 | Fixed |
| BUG-056 | 2026-10-01 | scripts/Presentation/CardFace/CardFace.cs / 卡牌插画占比 | 插画只在中部画框显示，紧凑卡面占比还随效果行数改变，无法覆盖整张卡牌。 | 标题、画框和底栏按纵向分区，LayoutCompact从插画区域扣除信息高度。 | 插画及其容器统一铺满卡面，信息和边框置于上层；去掉不透明纸纹，使用半透明底板与文字描边。基准三尺寸、全17卡池及1280棋盘截图已核对，117/117通过。 | Fixed |
| BUG-057 | 2026-10-01 | scripts/Presentation/CardFace/CardFace.cs / 基准卡面叠加顺序 | 400高基准卡面的效果和价值被金色边框覆盖。 | 信息ZIndex仅在紧凑布局设置，基准布局信息与装饰同层，底部间距不足。 | 在组件初始化统一抬高标题/效果/价值层级；为基准效果及价值保留安全间距，三尺寸原生截图核对可读。 | Fixed |
| BUG-058 | 2026-10-01 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 卡牌详情 | 兽皮、钻石的获得效果及珠宝袋、百宝箱的出售效果未显示；狮鹫的事件触发效果显示为泛化被动光环。 | 详情只按战斗能力枚举拼接文本，正式描述未进入属性与快照，无法完整表达获得、出售及事件触发条件。 | 增加只读 Description 属性，同步17张正式卡牌与CSV，经实例/商店/对局快照传给详情；按效果语义整理发动、回响、光环。构建通过、118/118验证通过，三类描述原生截图核对无裁切。 | Fixed |
| BUG-059 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` | 新增莫娜后6项依赖帕拉帝恩卡池的验证失败。 | 用例按列表下标选择英雄，按key排序后第一位变为尚无专属卡牌的莫娜。 | 相关验证显式选择 `hero.paladin`，消除英雄排序依赖。 | Fixed |
| BUG-060 | 2026-10-02 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / Details | 大教堂、狮鹫、铁匠铺等卡牌显示无关的攻击0，无发动能力的卡牌仍显示冷却占位值。 | 详情无条件遍历 CurrentValues，没有根据能力判断属性用途；内容中的占位字段被当作有效展示属性。 | 仅有 Active 能力时显示冷却，仅伤害效果读取攻击属性时显示攻击；保留真实零值攻击，不修改属性或战斗规则。新增辅助卡、材料卡、零攻击及护甲公式回归检查。 | Fixed |
| BUG-061 | 2026-10-02 | `scripts/Presentation/PhaseOneVerification.cs` / 拖拽出售检查注册 | 新增场景验证首次构建报 CS0027。 | 在字段初始化表达式中用 lambda 捕获 this，C# 不允许此处引用实例。 | 按现有场景验证方式在 _Ready 中注册检查。 | Fixed |
| BUG-062 | 2026-10-02 | `scripts/Domain/Combat/AbilityDefinitions.cs` / `BattleStateRecorder.cs` | 布尔状态接入首次构建失败。 | 新增状态key校验缺少 Common 引用，快照对象初始化前多保留一个 Select 闭合括号。 | 补齐引用并修正构造／初始化表达式，构建0警告0错误。 | Fixed |
| BUG-063 | 2026-10-02 | `scripts/Presentation/Verification/CardStateChecks.cs` / BerserkActiveOnly | 狂暴被动与周期回归首次失败。 | 夹具忽略当前多重也重复战斗开始被动，低估初始伤害与中毒量。 | 保留现行多重规则，按两次未加成被动及14点周期中毒核对，并另验回响不加成。 | Fixed |
| BUG-064 | 2026-10-02 | `scripts/Content/Cards/HolyGriffinCardDefinition.cs` / 发动描述 | 神圣狮鹫疾速文案限定人类，但实际作用于全部直接相邻己方卡牌。 | 文案未与通用相邻效果的实际筛选规则一致。 | 按确认的玩法保留全部相邻己方目标，同步定义与CSV描述，增加非人类邻居验证。 | Fixed |
| BUG-065 | 2026-10-02 | `scripts/Presentation/Verification/SmallRedPotionChecks.cs` | 红药水验证首次构建失败。 | 发动事件来源字段误写为SourceId，实际字段为SourceCardId。 | 修正事件字段引用并重新构建与运行验证。 | Fixed |
| BUG-066 | 2026-10-02 | `scripts/Application/Combat/BattleSetupFactory.cs` / 任务冷却 | 冷却属性任务加成改变展示属性，但原主动能力仍使用定义冷却。 | 冻结战斗输入时未把有效冷却属性差值传入主动能力。 | 使用通用CardAbilityComposer合成任务效果与有效冷却，验证原木法杖任务后首发为5秒且展示一致。 | Fixed |
| BUG-067 | 2026-10-02 | `scripts/Presentation/Verification/LogStaffChecks.cs` | 原木法杖验证首次构建失败。 | 伤害事件数值字段误写为Amount，实际为RawDamage。 | 修正引用，构建与143项回归通过。 | Fixed |
| BUG-068 | 2026-10-03 | `scripts/Presentation/Verification/CombatChecks.cs` / 状态周期验证 | 新状态周期验证首次构建失败。 | 实际事件Tick为long，预期元组数组推断为int，SequenceEqual类型不匹配。 | 显式声明预期元组Tick为long，构建通过。 | Fixed |
| BUG-069 | 2026-10-03 | `scripts/Presentation/Playtest/MatchPresenter.cs` / 怪物奖励提示 | 引入经验升级后，跨升级阈值的怪物奖励可能显示负经验。 | 原提示直接用结算前后的经验余数相减，忽略升级消耗。 | 提示将等级增加折算为10经验后再计算本次获得量，实际经验由统一入口升级并保留余数。 | Fixed |
| BUG-070 | 2026-10-03 | `scripts/Domain/Combat/BattleStatusResolver.cs` / 日蚀伤害 | 日蚀伤害绕过护甲，与确认的护甲优先规则不符。 | 日蚀调用通用伤害结算时将 BypassArmor 设为 true。 | 双方日蚀伤害均改为 BypassArmor=false，沿用伤害曲线、频率和日蚀来源标记；同步玩法及术语文档。 | Fixed（未运行对战验证） |
| BUG-071 | 2026-10-03 | `project.godot` / 游戏窗口显示 | 用户报告运行游戏时肉眼可见黑色闪烁，电脑录屏未捕获；关闭HDR后仍发生，用户确认未启用G-Sync。 | 用户确认切换Vulkan后不再闪烁，问题与当前环境的DX12渲染路径相关；具体驱动或显示链路根因未确认。 | 保持HDR输出关闭，Windows渲染驱动使用Vulkan；用户重启Godot运行后确认不再闪烁。 | Fixed（用户确认） |
| BUG-072 | 2026-10-03 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / 插画验证 | 未配置插画的合法卡牌无法通过验证，尽管卡面已有缺图占位。 | 验证强制要求每张卡牌配置正式原画，未覆盖空插画标识的展示契约。 | 空插画验证占位纹理与身份传递；已配置原画继续校验资源路径和尺寸。 | Fixed |
| BUG-073 | 2026-10-03 | `scripts/Domain/Combat/AbilityDefinitions.cs` / 冷却光环配置验证 | 新机制首次构建失败。 | 引用了不存在的GameElements.IsKnown方法。 | 改为沿用效果定义的非空元素key校验，元素身份仍由正式卡牌定义验证。 | Fixed |
| BUG-074 | 2026-10-03 | `scripts/Presentation/Playtest/KeyedActionView.cs` / 英雄缩略图初版 | 初版构建失败。 | 将Godot的icon_max_width主题常量误用为Button属性。 | 修正后按用户要求将英雄选角改为独立原画浏览组件，不再使用按钮图标。 | Fixed |
| BUG-075 | 2026-10-03 | `scripts/Presentation/Playtest/PlayerHeroPanel.cs` / 姓名布局 | 原画接入后英雄姓名未在截图中显示。 | 自动换行且裁切的Label在HBox内未得到有效最小高度。 | 为姓名设置48像素最小高度及垂直居中，1280×720截图确认恢复显示。 | Fixed |
| BUG-076 | 2026-10-04 | `scripts/Application/Economy/ShopCardPoolService.cs` / 商品等级筛选 | 商店首批商品及刷新可能出现高于商店等级的卡牌。 | 可售池只按英雄归属和尺寸过滤，报价直接采用卡牌初始等级，没有检查商店等级上限。 | 共用可售池增加初始等级≤商店等级条件，首批单件、多件及刷新统一使用过滤后的池；刷新资格按合格候选数判断，同步规则及边界/空池回归。 | Fixed |
| BUG-077 | 2026-10-04 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / 遭遇截图导出 | 新遭遇截图中原画与晶体比源图明显偏暗。 | HDR 2D 视口读回线性色彩，直接保存为普通 PNG，没有转换到 sRGB。 | 遭遇截图保存前按 HDR 视口转换像素到 sRGB，再导出 RGBA8 PNG；复查两档分辨率。其他历史截图入口另行统一。 | Fixed |
| BUG-078 | 2026-10-04 | `scripts/Presentation/Playtest/LeavePanel.cs` / `PlayerHeroPanel.cs` | 侧栏布局初次截图中阶段按钮只剩边框，英雄姓名被压缩。 | 横向容器内启用文字裁切，控件最小宽度不再由文字提供。 | 阶段按钮按实际字体测量设最小宽度，英雄姓名保留120px最小宽度；多档窗口及大窗缩回小窗检查姓名、按钮和购买区可见性。 | Fixed |
| BUG-079 | 2026-10-04 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / CaptureEncounters | 新增回放截图初版实际仍显示普通回合商店。 | 截图流程假定选择下一回合首个候选一定进入战斗，未核对页面类型。 | 按正式排程推进并检查Preparation后再启动播放，重建并检查战斗准备与回放截图。 | Fixed |
| BUG-080 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` | 旗帜回归初版不能编译。 | 将CardBattleSnapshot的Values误写为CombatValues。 | 使用实际Values字段读取冻结属性，并在构建成功后运行回归。 | Fixed |
| BUG-081 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` / Battle夹具 | 旗帜治疗断言比预期少1生命。 | 夹具默认在最后一刻触发日蚀，混入无关伤害。 | 日蚀起点设到夹具超时之后，独立验证光环效果；正式日蚀规则及战斗基线不变。 | Fixed |
| BUG-082 | 2026-10-04 | `scripts/Domain/Combat/BattleEffectResolver.cs` / AlliedAttributeAuras | 新光环初版给无对应属性能力的卡牌也显示加成，与属性修改规则不一致。 | 光环只检查所属方与存活/战场状态，没有检查目标支持的属性。 | 按SupportsCombatAttribute筛选目标，治疗效果注册治疗加成支持；使用有攻击能力的光环来源验证包含自身，召唤和叠加验证继续保留。 | Fixed |
| BUG-083 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` / 旗帜手验证 | 新验证首次构建失败。 | 将属性变化事件Amount误写为Delta，且Tick预期数组使用int。 | 改用Amount及long数组，与领域事件契约一致。 | Fixed |
| BUG-084 | 2026-10-04 | `scripts/Presentation/Verification/OrderPlateArmorChecks.cs` | 秩序板甲验证首次构建失败。 | 缺少Domain.Definitions命名空间，无法解析尺寸、元素与标签。 | 补充using，构建和167项回归通过。 | Fixed |
| BUG-085 | 2026-10-08 | `.codex/skills/project-star-dev-git/SKILL.md` / 拉取完成判定 | 本地 HEAD 与缓存 origin/dev 同为4c2538a时误报已同步，直接查询服务器后发现远端为e7da243、本地落后22个提交。 | 完成判定只比较本地引用，缺少服务器分支SHA的独立核验；首次fetch未反映最新状态的具体原因未确认。 | 拉取最终必须使用ls-remote查询服务器并核对服务器、缓存远端与HEAD三个完整SHA；不一致时有限重试，失败或仍不一致时禁止报告已同步；推送后同样直接核验。同步修正skill中的过期项目路径。 | Fixed（工作流规则已更新） |
| BUG-086 | 2026-10-08 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 元素倍率说明 | 新增说明初稿引用不存在的 `GameElements.DisplayName`，源码检查时发现会阻止构建。 | 将元素合法性定义误当作展示名称接口。 | 在表现层格式化已知元素的中文名；构建0警告0错误，184/184回归通过，说明验证包含光属性及恶魔/亡灵条件。 | Fixed |
