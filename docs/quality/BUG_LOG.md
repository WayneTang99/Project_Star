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
| BUG-087 | 2026-10-08 | `BoardZoneView.cs` / `MatchShell.cs` / 棋盘比例 | 卡面在占格按钮内留有空隙，窗口越宽越容易卡格不贴合。 | 骨架分别分配段高和宽度，格高取剩余段高，卡面再按比例居中缩小。 | 骨架以格宽和视口高度共同约束三段；统一格高为两倍格宽，卡面覆盖完整占格矩形，落点和预览使用同一边界。 | Fixed |
| BUG-088 | 2026-10-08 | `CardFace.cs` / 上下信息栏 | 不同效果数量改变底栏高度，且上下栏不等高。 | 底栏按纵向效果行数增高，顶栏独立计算。 | 共用同一栏高，效果横向排列；紧凑卡面优先展示能读清的效果，其余由提示和详情完整承载。 | Fixed |
| BUG-089 | 2026-10-08 | `PlaytestVerification.cs` / 重开夹具 | 布局首轮回归183/184，场景重开检查失败。 | 重开已移入开发折叠区，夹具仍直接触发隐藏按钮，被正常输入门控拒绝。 | 夹具先打开开发工具再点击重开，保留隐藏按钮不执行的门控。 | Fixed |
| BUG-090 | 2026-10-08 | `ShopView.cs` / 商品滚动条 | 首次实际截图出现多余竖滚动条。 | 商品行最小高度与容器测量造成非必要纵向溢出。 | 商品统一在固定阶段内布局，关闭纵向滚动，单独预留购买、失败原因和反馈空间。 | Fixed |
| BUG-091 | 2026-10-08 | `ResultView.cs` / 奖励图卡 | 结算图卡初次构建失败。 | 新组件遗漏卡面适配器所在命名空间。 | 补充Presentation.CardFace引用并重新构建。 | Fixed |
| BUG-092 | 2026-10-08 | `BoardZoneView.cs` / `PlaytestVerification.cs` / 小窗几何 | 几何和窗口验证因约0.00003px的负坐标或零点比较失败。 | 非整数格宽乘回总宽产生浮点误差，零坐标断言比其他边界断言严格。 | 格位左边界钳制到0，等尺寸卡面归零定位，几何检查使用0.1px容差，并继续检查四档窗口及缩回小窗。 | Fixed |
| BUG-093 | 2026-10-08 | `ShopView.cs` / `ResultView.cs` / 大窗缩回 | 从2560×1440切换到2560×1080时购买按钮超出阶段区，结算奖励容器也使用同一测量模式。 | Disabled滚动容器把旧商品行高度计入自身最小高度，内容又读取容器高度，形成不能缩小的测量循环。 | 两处改用ShowNever，隐藏竖滚动条但允许视口独立缩小，商品行和奖励行按阶段可用高度布局；复验大窗缩回与全部购买按钮基线。 | Fixed |
| BUG-094 | 2026-10-08 | `PlaytestVerification.cs` / 结算图卡领取夹具 | 新增结算图卡领取检查后184/185回归失败。 | 夹具主动关闭奖励浮层，仍用原断言要求浮层保持可见。 | 改为检查关闭状态不被领取刷新改变，并保留重复旧按钮不会再次领奖的验证。 | Fixed |
| BUG-095 | 2026-10-08 | `ShopView.cs` / 三尺寸截图 | 大型商品未显示合并箭头，只有小中型显示。 | 元信息侧栏的宽度门槛过大，没有为大型商品预留固定可读空间。 | 同高商品按最大尺寸预留76px侧栏预算，三尺寸均展示冷却及合并目标等级，保持购买按钮同基线。 | Fixed |
| BUG-096 | 2026-10-08 | `MatchPresenter.cs` / `TopBar.cs` / 当前回合标记 | 第1回合商店标成第2回合，第4回合怪物战标成第5回合；第8回合可能提前显示下一轮。 | 选择遭遇时领域已推进排程，顶栏直接展示最新领域轮回合，没有保留当前访问身份。 | 页面模型捕获进入遭遇前的轮回合，顶栏在访问和结算期间保持该标记；返回选择后显示下一回合，不修改领域排程。回归覆盖商店、怪物战及第8回合跨轮。 | Fixed |
| BUG-097 | 2026-10-08 | `scripts/Application/Match/MatchSnapshot.cs` / `CopyAbilities` | 新增传动齿轮后，卡面、插画及词条快照的三项回归抛出相邻回响配置异常。 | 能力快照重建遗漏新增的 `TriggerCardSide`，带方向的回响在复制时丢失必要配置。 | 补齐触发方向的冻结传递；分级验证使用复制后的能力执行战斗，全部内容身份、插画及词条快照回归通过，189/189全量通过。 | Fixed |
| BUG-098 | 2026-10-08 | `scripts/Presentation/Playtest/BattlePlaybackPresenter.cs` / `Project` | 临时转变出的登神者解锁元素任务后，Runtime已改为光属性，回放仍显示转变时的无属性并清空任务。 | 整卡转变投影提前返回，未继续应用新卡自身的冻结任务身份与进度。 | 转变投影继续读取冻结任务数据；新增达到60次阈值后显示光属性与任务进度、原对局卡仍为无属性且不累计临时成长的验证。构建0警告0错误，194/194全量回归通过。 | Fixed |
| BUG-099 | 2026-10-08 | `scripts/Application/Economy/CardEconomyService.cs` / `BuildMergePlan` | 连续合并后登神者任务看似重置，原高等级卡的永久成长、任务身份与宝石一并丢失。 | 合并链始终保留首个低等级目标，把已培养的高等级实例作为材料移除；此前任务验证只覆盖直接升级，未覆盖实际连续合并。 | 按用户确认每步保留原有更高等级卡，最终升级链中最高等级实例；补充正式购买路径的未完成/已解锁任务、永久成长、身份、位置和后续发动验证，同步宝石及满盘/未放置目标验证。构建0警告0错误，195/195全量回归通过。 | Fixed |
| BUG-100 | 2026-10-08 | `scripts/Presentation/Verification/AscendantChecks.cs` / `MergePreservesProgress` | 连续合并验证初版构建失败，随后零值多重读取及位置断言失败。 | 误用不存在的发动事件；假定属性快照必含零值 `Multicast`；把放入材料前的位置误当作合并前的位置，忽略正常棋盘推挤。 | 改用 `AbilityActivatedEvent.SourceCardId` 并排除回响；多重读取使用零值默认；在材料放置完成后捕获位置。 | Fixed |
| BUG-101 | 2026-10-08 | `scripts/Presentation/Verification/CardCatalogChecks.cs` / `Interaction` | 图鉴首次场景验证找不到卡牌列表节点。 | 验证路径假定未命名控件会使用类型名，Godot实际生成自动节点名。 | 浏览区显式命名Browser，验证使用稳定节点路径。 | Fixed |
| BUG-102 | 2026-10-08 | `scripts/Presentation/Verification/CardCatalogChecks.cs` / 搜索夹具 | 搜索验证与分尺寸截图未刷新列表，图片仍为全卡池。 | 夹具只直接设置LineEdit.Text，程序赋值不会发出用户输入的TextChanged信号。 | 设置文字后显式发送TextChanged，检查过滤结果与详情，并重新截图。 | Fixed |
| BUG-103 | 2026-10-09 | `scripts/Presentation/Verification/JadeToadChecks.cs` / 标签断言 | 验证初稿假定尺寸标签排在野兽标签前，源码核对发现断言会误判合法定义。 | 将无序标签集合按数组顺序比较。 | 改为检查标签数量及集合成员，不依赖枚举顺序；构建0警告0错误，209/209回归通过。 | Fixed |
| BUG-104 | 2026-10-09 | `scripts/Presentation/Playtest/HeroSelectionView.cs` / 名册缩略图 | 首次实际截图中英雄缩略图细节出现明显锯齿。 | 原画未生成mipmap，直接从1254像素缩至52像素显示，缺少足够的缩小预滤波。 | 表现层用Lanczos生成104像素缩略纹理并按插画key缓存，完整原画继续使用原资源；1280×720实际截图复查细节，四档窗口及缩回通过。 | Fixed |
| BUG-105 | 2026-10-09 | `scripts/Presentation/Playtest/CardCatalogView.cs` / 分类筛选初稿 | 源码核对发现初稿包含不存在的尺寸标签辅助方法，归属缓存局部变量与循环变量同名，会阻止构建。 | 未按实际接口名FromSize调用，并在同一作用域重复声明faction。 | 改用GameTags.FromSize，缓存变量命名为previousFaction；构建0警告0错误，209/209回归及两档实际图鉴窗口检查通过。 | Fixed |
| BUG-106 | 2026-10-09 | `scripts/Presentation/Verification/DuskSongJungleChecks.cs` / 流程夹具 | 首轮构建因不存在的Options字段失败，源码核对同时发现OptionsRevision误名。 | 未按实际页面模型的EventOptions/EventRevision字段读取选项及版本。 | 修正字段引用，构建0警告0错误，214/214回归通过，包含怪物战转场及回合保持检查。 | Fixed |
| BUG-107 | 2026-10-09 | `PlaytestText.cs` / `KeyedActionView.cs` / 加权事件说明 | 1280×720首张实际截图只显示砍伐的植物分支，50%怪物风险被挤到滚动区下方。 | 每个分支重复完整奖励句式并各占一行，默认16px按钮文本超过阶段区高度。 | 加权结果使用简洁说明，每行最多两个分支，多行按钮13px；实际截图确认两项及三种概率首次显示即可读全，原生点击和两档窗口通过。 | Fixed |
| BUG-108 | 2026-10-09 | `PlaytestVerification.cs` / `TestEncounterLevels` | 调整4级遭遇的验证初稿构建失败。 | 错误地从没有Level属性的EncounterDefinition基类读取等级。 | 保留原有普通排程隔离检查，断言当前正式候选未被统一覆盖为4级。 | Fixed |
| BUG-109 | 2026-10-09 | `scripts/Presentation/Verification/MagicCauldronChecks.cs` / `ThresholdAndMerge` | 魔法坩埚任务回归在快照冷却检查处抛出多元素异常。 | 任务解锁后快照同时包含主动施毒和被动减冷却能力，验证错误地假定仅有一个能力。 | 按Active筛选主动能力后检查5秒冷却，保留任务完成、合并与区域迁移验证；构建0警告0错误，217/217全量回归通过。 | Fixed |
| BUG-110 | 2026-10-09 | `SilverNeedleGrassChecks.cs` / 治疗验证夹具 | 初稿构建因战斗开始枚举名不存在而失败，后续临时诊断字符串也有转义错误。 | 错用OnBattleStart，并误对插值表达式内部引号转义。 | 改用PassiveOnBattleStart并移除临时诊断，保留实际治疗和冻结输入检查。 | Fixed |
| BUG-111 | 2026-10-09 | `SilverNeedleGrassChecks.cs` / 卡面治疗断言 | 首轮回归误判银针草出售后的治疗显示。 | 正式卡详情保留分级描述，验证却假定详情内含动态治疗句式。 | 改为校验FaceEffects的实际治疗数值，同时保留战斗治疗和冻结快照断言。 | Fixed |
| BUG-112 | 2026-10-09 | `BattleRuntime.cs` / CardBattleState治疗属性初始化 | 银针草出售后卡面显示治疗增加，但实际战斗仍使用原治疗量。 | 初始化治疗能力只登记HealingBonus为支持属性，未读取冻结CombatValues中的永久贡献。 | 从战斗输入复制HealingBonus，验证各等级实际治疗提升、旧输入不变和升级保留。 | Fixed |
| BUG-113 | 2026-10-09 | `BattleEffectResolver.cs` / 治疗上限 | 极大但合法的治疗量在满血上限裁定之前可能抛出整数溢出。 | 当前生命与治疗量以checked int相加，随后才取最大生命上限。 | 先用long累加并限制到最大生命，再转回int；验证合法int上限治疗将生命补至上限。 | Fixed |
| BUG-114 | 2026-10-09 | `BattleRuntime.cs` / 固定回放基线 | 永久治疗修复初版使无治疗加成的旧战斗记录出现额外零值属性键。 | 初始化对未配置HealingBonus的输入也写入0，改变了冻结状态字典。 | 仅复制输入中实际配置的HealingBonus，保留旧输入的字段集合及固定基线。 | Fixed |
| BUG-115 | 2026-10-09 | `scripts/Presentation/Verification/WhetstoneChecks.cs` / 快照入口 | 磨刀石验证初稿因引用不存在的快照工厂而构建失败。 | 未按现行MatchSnapshot.From入口获取快照。 | 改为MatchSnapshot.From，构建0警告0错误，磨刀石3项行为验证通过。 | Fixed |
| BUG-116 | 2026-10-09 | `scripts/Presentation/Verification/JailbreakerChecks.cs` / 奖励初值断言 | 越狱者奖励验证初稿把新局金币与经验视为零，误判正常奖励结算。 | CreateMatchService创建时已发放首轮收入和首回合经验，验证未计入这些初值。 | 记录战前资源，断言胜利后金币增加3、经验增加2，并保留奖励池等级与真实领取检查。 | Fixed |
| BUG-117 | 2026-10-09 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 随机持续状态技能说明 | 放逐说明会显示内部ImmobilizeDuration枚举和敌方英雄目标标题，无法正确说明双方随机卡牌禁锢。 | 随机卡牌效果直接插值状态枚举；能力标题仅读取通用Target字段，未反映组合效果的双方卡牌目标。 | 随机持续状态使用中文名称并显示目标数量；同时包含己方与敌方随机卡牌效果的能力标题显示双方战场卡牌，验证各等级说明；230/230回归通过。 | Fixed |
| BUG-118 | 2026-10-09 | `scripts/Presentation/Verification/BanishChecks.cs` / 命名空间 | 放逐验证初稿因缺少GameFactions所在命名空间而构建失败。 | 验证文件遗漏Domain.Definitions引用。 | 添加对应using；构建0警告0错误，230/230行为回归通过。 | Fixed |
| BUG-119 | 2026-10-09 | 小魔女卡牌原画 / 黑犀金龟、小型魔法药水、小型生命药水 | 三张小型卡牌仍引用方形原画，填满1:2卡面时横向裁切主体。 | 历史原画1254×1254与Definition的小型1:2比例不匹配。 | 内置图像工具重绘为887×1774，接入独立v2资源；9张小魔女现用插画比例全部匹配定义，实际卡面主体完整；构建0警告0错误，230/230回归通过。 | Fixed |
| BUG-121 | 2026-10-09 | `scripts/Presentation/Playtest/PaladinPortrait.gdshader` / 圣骑士选角动效 | 用户主要看到光点和亮度变化，人物缺少可见待机动作。 | 原动效仅对布料施加极小局部位移，头部、胸肩与持锤手没有动作，叠加光点成为主要运动。 | 改为轮廓蒙版驱动的呼吸、头颈轻摆、握锤同步运动及延迟布料摆动；随后按用户要求叠加跟随动作的圣光与少量光点。实际渲染验证人物运动、面部不受光效影响、外侧UI稳定及8秒首尾衔接，233/233回归通过。 | Fixed |
| BUG-120 | 2026-10-09 | `scripts/Presentation/PhaseOneVerification.cs` / 技能图鉴验证注册 | 图鉴验证初版构建失败。 | 字段初始化列表中的验证回调引用this，而实例此时尚未构造。 | 把需要真实控件宿主的原画检查注册到_Ready创建的场景集成分类。 | Fixed |
| BUG-122 | 2026-10-09 | HTML对局预览 / 棋盘卡面比例 | 草稿的棋盘宽度随窗口变化，卡高却按视口高度计算，造成小、中、大卡面比例偏离。 | 卡高与十格实际格宽使用不同的尺寸基准。 | 棋盘通过容器宽度及格间距计算统一卡高为两倍单格宽，三种尺寸共用占格规则；1280×720及1920×1080浏览器检查无横向溢出，实际截图已核对。仅涉及HTML预览。 | Fixed |
| BUG-123 | 2026-10-09 | ComponentShowcase.cs / CaptureEncounters 截图色彩 | Compatibility 截图底色、插画及面板发白，不能用于 HTML 对照。 | 仅判断 UseHdr2D，在 Compatibility 已为 sRGB 的读回图上再次做 LinearToSrgb。 | 增加实际渲染方法判断，仅对 Forward+/Mobile HDR 做转换；正式 Forward+ 截图已复查。 | Fixed |
| BUG-124 | 2026-10-09 | ShopView.cs / 宽屏商品购买基线 | 字号放大后不同商品购买按钮纵坐标不同，窗口检查失败。 | 每项单独按禁用原因是否可见预留空间，造成卡高及按钮基线不一致。 | 全商品共享最大原因留白，四档窗口及缩回检查通过。 | Fixed |
| BUG-125 | 2026-10-09 | PlayerHeroPanel.cs / 放大字号后的底栏 | 英雄姓名不显示，奖励按钮换行后覆盖经验并超出小窗。 | 名称行高低于字体真实行高；原按钮边距使三入口超过可用宽度。 | 调整姓名行高、资源条纵坐标、按钮内边距及成长摘要位置；1280×720 实际截图复查姓名和三入口可见。 | Fixed |
| BUG-126 | 2026-10-09 | CardFace.cs / 紧凑小型卡效果 | 放大字体后的 1280×720 截图中，两位攻击数值 10 被裁成 1。 | 效果区域被右侧价值牌挤压，按近似字符宽度计算的区域不足以容纳图标与两位数。 | 已采用真实文字测量与自然棋盘宽度；暂停前未补齐原问题小型卡攻击10的同尺寸截图验收，恢复后检查小商品重叠与长公式再关闭。 | Open |
| BUG-127 | 2026-10-09 | MatchShell.cs / HTML 视觉迁移 | 原生草稿只有大体配色和布局相似，字体、详情结构、间距及响应式效果与获批 HTML 不一致。 | 使用引擎默认字体和旧详情文本框，并以固定窗口高度约束代替 CSS 自然布局，未逐项测量对照。 | 已冻结基准并接入字体、结构化详情和自然滚动骨架；2026-10-10继续调整详情实测样式并修复刷新状态，功能与截图检查已推进。UI_HTML_PARITY_TODO.md记录剩余差异，完整视觉验收未完成。 | Open |
| BUG-128 | 2026-10-09 | MatchShell.cs / HDR 2D半透明UI | 面板、空格和遮罩实际颜色比HTML偏亮，透明度相同仍不一致。 | HDR 2D在线性空间合成透明层，浏览器CSS在sRGB空间合成；单纯修正截图转换无法修正合成后的颜色。 | 对局视口关闭UseHdr2D，截图直接读取sRGB；实际Forward+渲染复查透明底色，构建与234/234回归通过。 | Fixed |
| BUG-129 | 2026-10-09 | CardFace.cs / 等级与元素尺寸 | 设为HTML尺寸后元素徽章仍过大，等级位置也受旧尺寸影响。 | tscn保留的CustomMinimumSize覆盖代码设置的Size。 | 在布局及创建时清除旧最小尺寸，等级外接约35px、元素23px；两档截图复查角部与双元素。 | Fixed |
| BUG-130 | 2026-10-09 | CardDetailsContent.cs / CardDetailsView.cs / 详情尺寸与关闭 | 单段正文末尾被裁切、详情留下约160px空白，关闭按钮被拉伸／遮住；关闭后固定状态未释放，旧延迟回调在节点释放后报错。 | 正文高度按近似字符宽度估算，容器旧Size不会自动收缩；关闭按钮参与PanelContainer布局且TopLevel后绘制层低于面板；关闭只Hide，延迟闭包仍引用已释放控件。 | 按实际段落最小高度设置滚动区，详情及时收缩到内容高度；关闭按钮独立定位并使用高于面板的绘制层，通知MatchShell释放固定状态；移除高度延迟回调并保护定位回调。两档单／多段和属性展开截图复查，234/234回归通过且无运行异常。 | Fixed |
| BUG-131 | 2026-10-09 | ComponentShowcase.cs / HTML截图入口 | 初版截图入口将返回Task的方法直接注册Callable，调用时报Variant转换异常。 | Godot Callable不支持该Task返回值作为Variant。 | 使用async void入口包装等待截图任务；真实Forward+两档截图正常输出。 | Fixed |
| BUG-132 | 2026-10-09 | TopBar.cs / 菱形进度初始绘制 | 首帧菱形位置错误，出现在品牌区附近。 | 首次绘制发生于布局完成之前，后续尺寸更新没有重新绘制缓存。 | 监听ItemRectChanged并重绘；两档实际截图确认八菱形位于回合信息旁。 | Fixed |
| BUG-133 | 2026-10-09 | LeavePanel.cs / 阶段操作小窗 | 窗口检查失败，开发入口宽度被压缩并显示为空。 | 同行操作的最小宽度与主题边距冲突，开发按钮未预留可读宽度。 | 阶段按钮最小宽度40px，刷新／离开排序对应参考；四档窗口／键盘通过。小窗换行和局部滚动的视觉对齐仍属于BUG-127待办。 | Fixed |
| BUG-134 | 2026-10-09 | MatchTheme.cs / 字体与渐变初稿构建 | 初版字体间距和后续渐变字符串构建失败。 | 调用不存在的SystemFont.SetSpacing，且FormattableString缺少System命名空间。 | 使用FontVariation.SpacingGlyph，补充System引用；最终构建0警告0错误。 | Fixed |
| BUG-135 | 2026-10-09 | MatchShell.cs / ToggleEnemy | 展开敌方阵容后，没有对应的返回战斗摘要入口。 | 查看按钮位于BattleStage，展开时整个BattleStage被隐藏；没有在敌方棋盘提供返回动作。 | 敌方阵容内增加表现层“返回战斗摘要”按钮；展开／返回／再次展开检查通过，完整回归234/234。 | Fixed |
| BUG-136 | 2026-10-09 | MatchShell.cs / Render详情刷新 | 回放刷新会隐藏已打开的详情，鼠标未重新进入卡牌时无法恢复悬停提示；右键固定状态也被刷新重置。 | 每次Render直接Hide，并仅用SelectedCardId重置固定状态，没有保存有效悬停／固定卡牌身份。 | CardDetailsView保存当前卡牌身份；同页刷新时复用位置并重渲染新快照，卡牌不存在时关闭；出售／旧按钮连接回归通过，构建0警告0错误。 | Fixed |
| BUG-137 | 2026-10-09 | CardItemView.cs / 结构化战斗详情 | 回放状态说明追加到TooltipText，但新的结构化详情未读取这些说明。 | MatchShell中的_GetTooltip禁用旧提示，CardDetailsContent只收到CardSnapshot，没有接收战斗状态投影。 | 通过只读CardBattleSnapshot显示禁锢／疾速／迟缓／飞行／狂暴／摧毁和任务；2026-10-10补齐有到无状态及任务3/5到5/5解锁断言，两档实际截图确认旧状态移除、进度更新，234/234回归通过。 | Fixed |
| BUG-138 | 2026-10-10 | CardItemView.cs / 等级色卡框的交互轮廓 | 选中和键盘焦点的3px轮廓会覆盖2px等级框；悬停统一蓝色也改变了等级边缘。 | 交互轮廓在卡框原边界内绘制，未区分等级框与外部交互强调。 | 悬停使用等级颜色，选中／焦点轮廓向外扩展，保留等级框；构建、234/234回归及四档窗口／焦点检查通过。 | Fixed |
| BUG-139 | 2026-10-10 | CardDetailsView.cs / CardDetailsContent.cs / MatchShell.ScrollChanged | 同卡刷新让详情每次向右偏移16px、淡入重启并收起实例属性；窗口变化触发滚动后固定详情隐藏，下一次刷新重新打开。 | RefreshCard复用ShowCard导致新展示的偏移／Tween／展开重置；滚动回调无条件隐藏详情。同步复现233/234失败，窗口检查进一步复现隐藏问题。 | 分离新展示与快照刷新，刷新保留位置／Tween／展开及正文高度；同一选中卡复用刷新，旧定位回调按展示版本失效；滚动只隐藏非固定详情。修复后234/234及四档窗口／缩回／键盘检查通过。 | Fixed |
| BUG-140 | 2026-10-10 | PlaytestVerification.WindowsAndKeyboard / 长详情夹具 | 首轮长正文窗口检查在1920×1080误报缺少滚动。 | 8段正文在该窗口能完整容纳，验证错误地要求所有窗口均出现滚动条。 | 使用20段长说明确保四档窗口均超过正文预算，再实际滚动并验证不越界、无主动冷却及短正文收缩；四档窗口／缩回检查通过。 | Fixed |
