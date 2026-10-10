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
| BUG-126 | 2026-10-09 | CardFace.cs / 紧凑小型卡效果 | 放大字体后的1280×720截图中，两位攻击数值10被裁成1。 | 效果区域被右侧价值牌挤压，按近似字符宽度计算的区域不足以容纳图标与两位数。 | 使用真实字体测量；2026-10-10补齐真实2级小型护腕AttackDamage=10、damage图标、文本10与18px字宽／18px可用宽断言，四档原生截图通过。原问题1280×720证据见UI_PARITY_CONTINUE_2026_10_10。 | Fixed |
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
| BUG-141 | 2026-10-10 | CardItemView._MakeCustomTooltip / MatchShell.ShowHoverCard | 用户截图显示神圣狮鹫和登神者同时出现两份详情，左侧悬停窗重复右侧详情。 | 卡牌同时提供引擎自定义Tooltip和MatchShell统一详情，两条路径均绘制CardDetailsContent。 | 删除卡牌引擎自定义Tooltip并返回空提示，统一通过CardDetailsView悬停／固定／刷新／关闭；商品与奖励也复用同一面板。236/236回归及三档原生截图通过，见UI_CARD_INTERACTION_PLAN.md。 | Fixed |
| BUG-142 | 2026-10-10 | 独立HTML原型 / drawBoards | 静态检查发现原型卡面函数新增动作参数后，棋盘map调用会把数组索引误传为交易动作。 | Array.map直接传入cardHTML，会同时传递索引和原数组。 | 棋盘映射改为显式只传卡牌key的回调，保持棋盘点击为详情；仅修改HTML原型。 | Fixed |
| BUG-143 | 2026-10-10 | 独立HTML原型 / 任务进度预览 | 静态检查发现进度切换按钮展示详情后会立即关闭详情。 | 点击事件继续冒泡到页面空白点击关闭处理器。 | 预览按钮停止冒泡后更新任务进度，保留同一个详情容器；仅修改HTML原型。 | Fixed |
| BUG-144 | 2026-10-10 | 独立HTML原型 / 悬停详情点击命中 | 连续购买检查发现悬停详情覆盖邻近商品，下一次点击未触发购买。 | 未固定的悬停面板也消费鼠标事件，向左避让时覆盖其他卡牌。 | 未固定详情透过鼠标命中卡牌；仅固定详情接收鼠标操作，关闭按钮仍可点。仅修改HTML原型。 | Fixed |
| BUG-145 | 2026-10-10 | ComponentShowcase.cs / 详情事件订阅 | 卡牌详情事件加入邻接定位边界后，组件展示入口构建失败。 | 展示入口仍订阅旧的单参数ShowDetails。 | 同步为卡牌快照与Rect2双参数，并使用ShowNear定位；构建0警告0错误，236/236回归通过。 | Fixed |
| BUG-146 | 2026-10-10 | CardInteractionChecks.Capture / 原生输入夹具 | Escape关闭详情后的购买捕获访问已释放商品节点。 | 取消选择会通过Presenter刷新商店并替换商品控件，夹具继续引用旧节点。 | Escape后重新查询同报价的卡牌节点，再注入鼠标输入；实际鼠标／Enter购买和Space奖励领取通过。 | Fixed |
| BUG-147 | 2026-10-10 | CardDetailsContent / 任务节点名称 | 独立任务区首轮回归无法按任务名称找到节点，234/236通过。 | 直接使用含点号的任务key作为Node.Name，Godot校验节点名称后路径不匹配。 | 节点名称将点号替换为下划线，任务数据关联仍使用原StringName key；236/236通过。 | Fixed |
| BUG-148 | 2026-10-10 | PlaytestVerification.IsolatedComponents / 失效详情断言 | 允许商店详情同页刷新保留后，旧断言仍期待详情关闭，235/236通过。 | 夹具只移除了库存卡牌，该卡牌仍在商品列表中；因此并未真正失效。 | 同时清空商品列表，验证有效报价详情保留与真正移除后关闭；236/236通过。 | Fixed |
| BUG-149 | 2026-10-10 | CardInteractionChecks.Capture / 窄窗证据 | 615×440捕获实际仍为1280×720，并覆盖同名截图。 | 试玩入口设置了1280×720窗口最小尺寸，夹具未解除限制就按请求尺寸检查。 | 仅在截图夹具解除最小尺寸；实际615×440截图复查正文滚动及详情边界，正常试玩最小尺寸不变。 | Fixed |
| BUG-150 | 2026-10-10 | CardDetailsView / 悬停鼠标透过 | 静态复查发现仅忽略面板根节点仍可能让未固定详情遮挡商品点击。 | 外层Content容器及每次Render新建的任务面板仍使用默认MouseFilter，旧的忽略设置未覆盖新子节点。 | 每次刷新后按固定状态递归设置整个Content，关闭按钮保持可点；检查所有新任务节点透过、固定正文接收滚动，236/236通过。 | Fixed |
| BUG-151 | 2026-10-10 | CardInteractionChecks.Capture / 固定任务夹具 | 原生截图复查发现滚动任务后详情消失，旧检查只验证边界仍通过。 | 夹具直接打开详情，没有同步MatchShell的固定状态，卡牌MouseExited将其隐藏。 | 仅在只读视觉快照添加并选中示例卡，检查Visible后再保存；三档任务／蓝色／滚动截图通过，示例不写入正式对局。 | Fixed |
| BUG-152 | 2026-10-10 | ShopView / 窄窗键盘卡牌焦点 | 615×440原生检查中第三张商品仍在可见区域外，键盘焦点移到不可见商品。 | 未开启FollowFocus；启用后聚焦时横向滚动范围从732退为36，商品行动态布局的宽度未显式维持；夹具也曾在布局完成前设置焦点。 | 启用FollowFocus，按可见商品行宽与间距设置内容最小宽度；夹具等待布局再聚焦。三档原生焦点／悬停检查和四档窗口回归通过。 | Fixed |
| BUG-153 | 2026-10-10 | CardItemView / 大型卡牌悬浮复位 | 大型卡牌悬浮后顶到容器顶部，离开后不能恢复居中位置。 | 悬浮Tween将卡面绝对Y设为-6或0，覆盖宽高适配产生的居中偏移；刷新也会与Tween争写位置。 | Tween只更新独立悬浮偏移，LayoutFace统一计算居中位置加偏移；悬浮／离开／刷新／缩放验证、237/237回归、四档窗口和原生拖拽通过。 | Fixed |
| BUG-154 | 2026-10-10 | PlumedHelmetCardDefinition / 描述词条 | 羽饰头盔的相邻卡牌发动充能被标为光环，与术语表和实际回响触发不一致。 | 描述使用CardKeywords.Aura，能力实际为EchoOnAdjacentAlliedCardActivated。 | 已将正式Definition描述改为CardKeywords.Echo；现有分级伤害、相邻人类充能、多重、即时队列与冻结配置检查全部通过，仅更改分类，不改能力。 | Fixed |
| BUG-155 | 2026-10-10 | combat-status.css / HTML状态预览 | 切换到无持续伤害的示例仍显示灼伤／中毒徽章。 | .dot-row的display:flex覆盖浏览器对hidden属性的默认display:none。 | 显式设置.dot-row[hidden]为display:none，浏览器复查正常与非战斗页均隐藏；仅修改HTML设计草案。 | Fixed |
| BUG-156 | 2026-10-10 | combat-status.css / HTML怪物状态区 | 桌面截图中怪物的灼伤／中毒徽章被阶段底边裁切。 | 固定阶段高度容不下新增状态行和内边距。 | 战斗阶段使用自然高度和260px最小高度；宽窄屏检查状态行底边均在阶段边界内，截图复查完整可见。 | Fixed |
| BUG-157 | 2026-10-10 | game-16x9.css / HTML右侧信息栏 | 16:9首轮截图中三排棋盘完整可见，但右侧播放按钮超出画布。 | 怪物资源、英雄资源与阶段操作按自然高度叠加，未控制侧栏总高度。 | 限定942px侧栏，收紧资源／技能间距及肖像高度，压缩反馈文本，保留卡牌比例与字号；1920×1080浏览器实测播放按钮和等级图例均在画布内，截图通过。 | Fixed |
| BUG-158 | 2026-10-10 | game-16x9.js / HTML禁锢示例 | 点击禁锢示例后没有卡牌显示禁锢，野猪冷却继续推进。 | 按固定数组索引选择了没有主动冷却的兽皮，随后被无冷却过滤跳过。 | 禁锢示例明确选择预览野猪实例，不按无冷却卡牌的顺序定位；播放时两次浏览器读取均为3.6秒，禁锢标识可见。 | Fixed |
| BUG-159 | 2026-10-10 | game-16x9.html / 卡面设计范围 | 首版排版草案自行新增卡内名称，并改动卡框、元素、效果／价值位置，超出用户要求。 | 页面草案重新制作卡面，没有复用已有获批样式。 | 移除草案卡内名称和自定义卡框；逐字复用card-interactions.html卡面CSS与原结构，复用combat-status.css状态层，仅由容器控制排版。浏览器确认5张己方卡均保留card-art与value，无card-name；修订截图已复查。 | Fixed |
| BUG-160 | 2026-10-10 | game-16x9.css / 缩放画布焦点 | 小窗截图中顶部HUD和怪物战场上缘被裁切，内部卡牌边界检查仍通过。 | transform缩放不改变1920×1080排版盒，overflow:hidden父容器仍能因聚焦而滚动；内部game的装饰溢出也形成可滚动区域。只检查子元素位于game内，遗漏两层容器的滚动偏移。 | frame与game两层均使用overflow:clip；五档窗口及卡牌／播放按钮焦点实测确认frame在视口内、game与frame同起点、两层scrollTop均为0，HUD／三排棋盘／操作可见。1280×720修订截图已复查。 | Fixed |
| BUG-161 | 2026-10-10 | game-16x9.html / 整体设计范围 | 只恢复卡面后，HUD、英雄栏、资源条、技能、商品按钮、详情和开始页仍与原版不同，违背只改排版的要求。 | 16:9草案从头重做界面；上次修正只覆盖卡牌组件，未恢复其他组件。 | 整体恢复combat-status.html，只追加布局CSS／脚本；去掉两处引用后与原HTML逐字一致，布局CSS无字体／配色／边框覆盖。新设计产物撤回；五档窗口确认怪物信息在上、英雄在下，三排棋盘完整。原Enter购买、单一详情、三项任务和主题切换检查通过，无浏览器错误。 | Fixed |
| BUG-162 | 2026-10-10 | DisplaySettingsView / 可用分辨率 | 无边框窗口按工作区排除任务栏后，可能错误拒绝与屏幕原生尺寸相同的16:9分辨率。 | 首版统一使用ScreenGetUsableRect，未区分现有无边框窗口与带边框窗口。 | 无边框按ScreenGetPosition／ScreenGetSize检查并居中；带边框仍使用工作区。修正后构建与显示回退回归通过。 | Fixed |
| BUG-163 | 2026-10-10 | 获批HTML迁移 / 等级框与战斗反馈 | 宣布布局执行完成时，等级框扩展与新版战斗反馈仍停留在HTML，未完成获批方案。 | 按16:9子计划收尾，遗漏其引用的战斗反馈计划；功能回归不能验证未接入的视觉项。 | 补齐等级框、发动与状态层、双方持续伤害反馈，242/242及三档原生截图通过；整体字体／材质对齐仍按BUG-127单独验收。 | Fixed |
| BUG-164 | 2026-10-10 | CombatTraceChecks / 周期伤害来源元数据 | 新增伤害来源后旧战斗指纹检查失败，241/242。 | 指纹依赖record.ToString，新只读字段改变序列化文本，原伤害与快照并未改变。 | 旧基线继续比较原字段，不重写基线；新增来源由双方周期伤害专项检查验证，242/242通过，原三组基线一致。 | Fixed |
| BUG-165 | 2026-10-10 | HtmlParityCapture / 导师截图夹具 | 新增导师截图夹具首次构建失败。 | 夹具误从Definition根读取身份字段，并遗漏CardDisplayAdapter命名空间。 | 改为读取Attributes.Identity，补齐命名空间。 | Fixed |
| BUG-166 | 2026-10-10 | LevelPresentation / 容器详情等级框 | 原生截图发现详情等级框挤到正文内侧，与面板边界不重合。 | PanelContainer按内容边距排版普通子节点；独立层还需显式跟随原浮层的全局位置及绘制层级。 | 容器装饰独立于排版，跟随全局边界、层级和淡入；几何／层级断言、三档截图与窗口检查通过，无新增锚点警告。 | Fixed |
| BUG-167 | 2026-10-10 | DisplaySettingsView / 设置弹窗键盘焦点 | 复查发现设置没有独立Tab焦点环，键盘可跳到弹窗后选角控件；重复Open会丢弃预览状态。 | 弹窗只拦截鼠标，未设置焦点关系，Open无重入保护。 | 设置／确认两个阶段均配置正反Tab循环，重复Open保持当前预览；焦点和取消恢复断言通过，242/242回归通过。 | Fixed |
| BUG-168 | 2026-10-10 | HtmlParityCapture / 导师候选等级 | 新导师截图夹具直接复用玩家已获技能，候选等级与导师访问等级不一致。 | 图形夹具绕开导师用例，把库存当作传授候选。 | 改为在隔离夹具对局通过MentorService.Open捕获正式筛选候选，同次访问等级统一且不足三项不补齐；不修改正式内容。 | Fixed |
| BUG-169 | 2026-10-10 | HtmlParityCapture / 战斗数值夹具 | 状态截图里部分卡牌攻击／护甲／中毒显示0，与战前样例不一致。 | 夹具把CardBattleSetup独立数值设为0，覆盖了CombatValues，同时没有传入实例价值、标签和多重。 | 正确传入卡牌冻结数值及价值，重新生成三档状态截图；不改模拟器或正式定义。 | Fixed |
| BUG-170 | 2026-10-10 | CardDetailsView / 原型动效和内边距 | 详情缺少获批HTML的6px位移淡入，正文比HTML宽2px，阴影仍采用旧版本参数。 | 只迁移透明度；等级边框从1px增至2px后仍沿用21px内容边距和旧阴影。 | 分离最终定位与临时位移，恢复6px／120ms淡入；边框加正文留白共22px，采用新版12px偏移阴影。243/243、原生输入及四档截图通过；精确阴影边缘仍属BUG-127。 | Fixed |
| BUG-171 | 2026-10-10 | CardDetailsView / 16:9伸缩 | 1920×1080原生详情宽456px，而获批HTML固定为380px；不同分辨率下字体、徽章和间距随画布缩放。 | 固定在body上的HTML浮层被直接当作画布组件等比放大。 | 详情反向抵消画布伸缩，以屏幕像素排版；定位间距、淡入位移、关闭按钮和等级框同步换算。四档实际窗口及改变／恢复分辨率检查通过。 | Fixed |
| BUG-172 | 2026-10-10 | MatchShell / 商店与遭遇排版 | 原生阶段标题独占左栏，商品／选项占右栏；获批HTML的阶段标题和内容应同在右侧阶段面板，左栏显示英雄。 | 把怪物摘要的左栏位置错误地推广到所有阶段，英雄始终留在左下。 | 普通阶段的原标题组件移入右侧面板左段，英雄回到左上；怪物仍在左上、英雄在其下，保留原组件样式及操作。商品高度按HTML实际窗口vh计算。四档截图、原生卡牌输入与窗口／拖拽检查通过。 | Fixed |
| BUG-173 | 2026-10-10 | MatchShell / 两侧装饰线 | 左侧椭圆装饰压入英雄面板，切换怪物页后高度变化；右侧装饰越界。 | 使用阶段边界和英雄底边定位，原型实际以整个app的左右边缘及上下120px定位。 | 改为画布两侧16px外距、26px宽，上下120px留白；不改线条材质。绘制顺序也恢复到面板之前；四档最终截图复查通过。 | Fixed |
| BUG-174 | 2026-10-10 | CardDetailsView / 已固定详情的分辨率变化 | 改变画布伸缩后详情宽度恢复380px，但相邻16px间距仍沿用原缩放坐标。 | 仅换算浮层尺寸和边界，没有保留卡牌锚点用于重新计算相邻定位。 | 保存只读锚点，伸缩／边界变化时重新计算左右翻转和16px间距；原生截图夹具加入实际窗口变化及恢复断言。四档真实固定详情及分辨率变化／恢复检查通过。 | Fixed |
| BUG-175 | 2026-10-10 | HtmlParityCapture / 固定详情夹具 | 分辨率变化检查误报：宽度及间距正确，详情却被悬停退出清理关闭。 | 夹具直接调用ShowNear，未走右键固定入口，因此页面仍把它视作普通悬停。 | 使用真实卡牌右键输入设置页面固定状态，再改变窗口并核对身份、可见性、宽度和间距。四档捕获及实际缩放检查通过。 | Fixed |
| BUG-176 | 2026-10-10 | HtmlParityCapture / 像素尺寸断言 | 15.999976px间距被判为不等于16px，导致正确缩放检查失败。 | 使用通用浮点相等判断，其容差比真实绘制精度更严格。 | 像素宽度与间距采用0.05px容差，仍拒绝1px以上的可见偏移；身份、可见性和命令隔离断言保留。四档捕获及实际缩放检查通过。 | Fixed |
| BUG-177 | 2026-10-10 | ComponentShowcase / 截图入口 | 截图断言异常后隐藏进程持续运行，无法从退出码判断失败。 | async void入口未捕获异常并终止截图任务。 | 捕获异常、输出完整诊断并以非零退出码结束；成功路径仍由原截图流程退出。 | Fixed |
| BUG-178 | 2026-10-10 | MatchShell / 三排棋盘布局 | 原生三排按剩余高度等分，卡牌被高度预算压小；普通页己方战场比原型低约29px，备战区低约21px。 | 将阶段与两排棋盘强行等高，未按棋盘宽度、10格比例、27px标题和12px区域间距计算自然高度；阶段起点多1px。 | 按原型宽度推导棋盘高度，普通阶段214px、怪物阶段按自身棋盘高度，恢复136px起点及12px间距；243/243边界回归、四档截图、窗口／键盘及原生拖拽通过。 | Fixed |
| BUG-179 | 2026-10-10 | BoardZoneView / 怪物自身战场 | 怪物战场多显示一行“敌方战场”标题，卡牌因此向下偏移27px。 | 直接复用带标题的己方棋盘，未保留原型怪物阶段无标题的包装布局。 | 复用棋盘并仅关闭怪物包装内标题，卡面、槽位、详情与冻结回放不变；10格边界、实例复用与243/243回归、四档截图通过。 | Fixed |
| BUG-180 | 2026-10-10 | 统一HTML / 重建棋盘 | 首次合并后怪物页的己方卡牌没有发动／状态层，浏览器实测层数量0。 | 新样例初始化重绘棋盘后未重新投影既有表现状态。 | 重建棋盘后重新更新只读表现层；最终浏览器状态层5项、窗口及控制台复查通过。 | Fixed |
| BUG-181 | 2026-10-10 | 统一HTML / 拖拽卡面 | 拖拽预览从缩放画布移到body后，图标和文字会按未缩放尺寸绘制。 | 只复制卡牌的屏幕宽高，没有同步画布缩放，导致卡内装饰相对尺寸改变。 | 按逻辑尺寸复制原卡面并同步缩放、保留屏幕抓取偏移；1920／1280实际拖拽、移出取消和禁用检查通过。 | Fixed |
| BUG-182 | 2026-10-10 | 统一HTML / 详情样例 | 属性详情样例出现在窗口左上角，未紧邻原卡牌；数值10仍未着色。 | 以未挂树的克隆作为锚点；属性词语插入span后数值只读取紧邻文本节点，漏过空格。 | 使用不可交互的实际定位锚点，按同段语义为数字赋色；浏览器确认详情紧邻卡牌、攻击与护甲的10分别着色，缩放后仍保持380px宽及约16px间距。 | Fixed |
| BUG-183 | 2026-10-10 | 统一HTML / 分辨率预览 | 设置的应用仅改说明文本与配色，没有可视化不同尺寸的画布预览。 | 初稿未将所选尺寸传入排版预览。 | 以虚拟显示区域呈现所选分辨率和留边，确认保留、取消／Escape／超时恢复；不改变浏览器或操作系统设置；预览／Escape／超时回退检查通过。 | Fixed |
| BUG-184 | 2026-10-10 | CardItemView.DragPreview / 原生出售拖拽 | 用户报告出售拖动时卡面没有跟随鼠标。 | DragPreview只创建卡名Label，没有复用卡面。 | 获批后复用完整CardFace、原缩放与抓取偏移；战场／备战真实鼠标位移、Escape取消及重新出售通过，实际截图卡面可见，保留既有出售命令与金额校验。 | Fixed |
| BUG-185 | 2026-10-10 | 统一HTML / 导师样例 | 初稿把无阵营冲撞误作为2级圣骑士技能，令大主教在2级出现额外候选。 | 复用英雄展示栏示例而未核对正式归属／初始等级；冲撞实际无阵营、1级。 | 大主教1级只捍卫、4级捍卫与至圣斩；三选一使用单独不限阵营排版示例，图鉴标明归属。正式归属、1项／2项／3项示例、空候选与一次领取复查通过。 | Fixed |
| BUG-186 | 2026-10-10 | 统一HTML / 弹窗取消焦点 | 设置Escape关闭后焦点仍停留在隐藏的“保留”按钮。 | 在cancel事件默认关闭之前聚焦弹窗外按钮，被浏览器模态焦点门控拦截；超时回退也会隐藏当前确认按钮。 | 取消事件阻止默认关闭后显式关闭／恢复菜单焦点，回退先聚焦应用；菜单正反Tab、Escape和超时检查通过。 | Fixed |
| BUG-187 | 2026-10-10 | 统一HTML / 空备战详情样例 | 静态复查发现卖完备战卡牌后点击详情样例会空引用，虚拟详情锚点也未跟随画布缩放。 | 样例假定备战始终存在卡牌，隐藏锚点使用一次性的屏幕坐标。 | 允许用空格锚点，定位时同步真实棋盘边界；空备战、长正文及实际1280→1920相邻定位复查通过。 | Fixed |
| BUG-188 | 2026-10-10 | 统一HTML / 开始界面弹窗返回 | 静态复查发现放弃后进入设置或选角再取消，会露出已放弃的旧对局预览。 | 打开子弹窗前提前关闭开始／终局父弹窗。 | 保留父弹窗层，取消子弹窗回到原入口；只有确认英雄时关闭父层开始新预览。放弃／开始／设置／选角的取消及重新确认流程通过。 | Fixed |
| BUG-189 | 2026-10-10 | 统一HTML窗口核对 / 读取时机 | 批量设置浏览器尺寸后立即读取DOM，1280窗口仍记录上一档1920画布；仅判断卡牌在画布内又会误判通过。 | 未等待浏览器新的UI状态，且漏查整个画布位于视口内。 | 每档尺寸后读取最新UI状态再测量，并明确核对画布四边、经济区和菜单均在视口内；四档画布四边、经济区、菜单、上下关系及无整页滚动通过。 | Fixed |
| BUG-190 | 2026-10-10 | DragSaleChecks.Native / 预览抓取断言 | 原生抓取位置检查因约0.014px差值失败。 | 源卡悬浮Tween仍在推进，断言将后续帧的源卡位置与拖拽开始时冻结的预览偏移作浮点完全相等比较。 | 按屏幕观感使用0.5逻辑像素定位容差，保留预览尺寸、20×10鼠标位移及取消／出售的独立断言。 | Fixed |
| BUG-191 | 2026-10-10 | CardEffectRow / CardValueBadge / 统一HTML | 用户指出生命药瓶文字与底板配色难看，先只改字色，又误将所有底板统一。 | 未确认用户要调整的字色／底色关系，先后错误解读需求。 | 用户确认底板使用各属性专属色、数字统一浅色。撤回统一底色，效果底板按对应专属色52%→28%渐变、价值底板为金色35%，数字浅象牙白。构建0警告0错误，HTML计算样式及1280原生两套主题截图通过；旧统一底色截图仅为被否决方案证据。 | Fixed |
| BUG-192 | 2026-10-10 | CardItemView.DragPreview / 原生出售截图 | 实际出售截图中拖动卡面被出售区遮住，坐标跟随检查却通过。 | 预览使用默认ZIndex=0，出售区ZIndex=20；只检查坐标与尺寸没有发现渲染层级遮挡。 | 预览使用独立ZIndex=100置于出售区上方；重新用原生输入与实际截图确认完整卡面可见、抓取跟随和取消／出售通过。 | Fixed |
| BUG-193 | 2026-10-10 | ResourceBar / HTML生命及魔法条 | 用户指出资源条仍为旧绿色／青蓝色底，叠加生命粉色／魔法紫色数字不协调。 | 属性色仅应用于数字和图标，未同步填充资产与HTML底色。 | 生命填充使用生命柔粉红、魔法填充使用淡紫的65%→40%渐变，数值统一浅象牙白；己方及敌方共用资源条同步。构建0警告0错误，1280原生两套主题／敌方摘要截图、窗口变化及恢复检查通过。 | Fixed |
| BUG-194 | 2026-10-10 | 统一HTML / 灼伤示例 | 用户指出灼伤没有体现在HTML中；虽有状态徽章，卡面没有灼伤图标、名称及直达示例。 | effectNames与SVG符号缺少burn，未设置灼伤卡面／详情夹具。 | 补全火焰图标、名称、卡面施加量、详情和0.6s持续伤害示例；仅UI夹具，不新增正式内容；待验证。 | In Progress |
| BUG-195 | 2026-10-10 | HTML / CardKeywordText / 再生术语分类 | 生命再生和魔法再生未明确区分，魔法再生被拆开着色。 | 原生词语匹配遗漏完整再生术语，魔法再生复用魔法色。 | HTML完整术语及对比已验证；项目优先匹配完整词语并关联独立属性key与颜色。两种再生的词语／数值截图、两套桌面主题及246项回归通过。 | Fixed |
| BUG-196 | 2026-10-10 | CardDetailsContent / CardDetailsView | 同内容叠加发现标签比HTML窄3px且矮2px、标题提前2px、正文高度重复计入留白、冷却圆徽右移4px、关闭按钮字号及位置不符。 | 标签未将边框计入内容留白，Godot与CSS字距／字体行盒不同；正文同时设置行距和上下留白；标题尾距与header右留白未精确迁移；关闭沿用12px默认按钮。 | 修正标签边距、标题行盒、正文预算、徽章留白与关闭按钮；四档实际截图、长正文、固定详情缩放、鼠标／键盘输入通过。 | Fixed |
| BUG-197 | 2026-10-10 | CardDetailsView / MatchTheme.DrawSurface | 详情原生阴影呈硬边黑框，淡入进度与HTML不一致。 | StyleBoxFlat软边并非CSS高斯模糊；Cubic Out不同于CSS默认ease曲线。 | 详情改为缓存柔和阴影及CSS ease曲线；中点进度、刷新／关闭重开、四档两套主题截图通过。微小栅格差异按用户最新要求不再追齐。 | Fixed |
| BUG-198 | 2026-10-10 | DisplayLayoutChecks.Settings | 清理output后全量回归245/246，显示设置保存检查失败，headless与原生均可复现。 | 验证夹具写入output/ui-16x9/settings-check.cfg前未创建目录，误将目录缺失视为设置逻辑失败。 | 验证开始时创建自身输出目录并检查返回值；246/246全量验证通过，正式显示设置逻辑未改。 | Fixed |
| BUG-199 | 2026-10-10 | PlaytestVerification.WindowsAndKeyboard | 详情Header新增右留白容器后窗口检查发生Node not found和空引用。 | 本轮组件层级变化后遗漏窗口验证中的两个旧冷却节点路径。 | 同步两处路径；窗口／焦点／键盘、无主动冷却、长正文滚动及边界检查重跑通过。 | Fixed |
| BUG-200 | 2026-10-10 | 详情对照捕获批处理 | 蓝色主题捕获日志仍为森林金，宽屏蓝色文件缺失。 | 批处理将附加参数数组放在原生命令的--之后，未正确展开为用户参数，未校验实际主题。 | 改为显式传入--blue-details并校验实际主题日志；四档两套主题重新捕获通过，宽屏使用独立窗口尺寸后缀。 | Fixed |
| BUG-201 | 2026-10-10 | Godot / 卡牌详情可读性 | 用户反馈项目详情文字过小，HTML原字号没有问题。 | 原生详情沿用12px正文及20px标题，固定屏幕尺寸使大窗口仍显小。 | 项目正文18px、标题24px、标签13px、辅助14px，浮层440px并保留滚动；三档实际详情截图与窗口检查通过，HTML原字号恢复。 | Fixed |
| BUG-202 | 2026-10-10 | Godot / 显示画布基准 | 用户反馈画面缩放后内容大小不协调。 | 当前项目设置缺少1600×900逻辑视口尺寸，偏离既有固定16:9画布约定。 | 恢复1600×900逻辑视口，实际窗口保持等比缩放与宽屏留边；窗口、缩回及键盘检查通过，HTML恢复原缩放。 | Fixed |
| BUG-203 | 2026-10-10 | HTML / PlayerHeroPanel / 英雄小头像 | 战斗小窗口使用整幅原画，脸部太小。 | 普遍使用cover；原生仅对竖图做统一方形裁切，未按主体脸部位置取景。 | 六位英雄按HTML已确认的脸部取景同步为原生AtlasTexture显示区域；原画与选角主图保留完整。六张项目实际截图检查通过，构建0警告0错误、246/246回归通过。 | Fixed |
| BUG-204 | 2026-10-10 | HTML / AttributePalette / 属性色 | 用户反馈恢复类属性等色号重复度过高。 | 生命／治疗／再生过于接近，原生魔法与魔法再生还共用同色，SVG底板和状态徽章残留旧色。 | 十种独立属性色同步项目，魔法再生独立蓝紫、生命再生青色、治疗翡翠绿、生命玫红；图标、详情、效果底板、资源条和状态徽章同步。两套主题实际截图及246/246回归通过。 | Fixed |
| BUG-205 | 2026-10-10 | HTML / 灼伤状态 | 灼伤衰减至0后仍显示“灼伤0”。 | HTML状态渲染未判定剩余量；原生已有amount大于0时显示的规则。 | HTML不大于0隐藏、重新施加恢复，真实衰减及双方隐藏检查通过；原生零灼伤隐藏的既有回归保持通过，无需修改战斗结算。 | Fixed |
| BUG-206 | 2026-10-10 | 统一HTML / 属性数值着色 | 配色截图发现生命再生10与魔法再生5被后文的生命／魔法染色，治疗20也被生命染色。 | 数值先向后寻找属性词，越过紧邻前方的完整属性名称。 | 优先使用紧邻前方的属性标签，后置数值继续保留原匹配；治疗／魔法／两种再生数值断言及恢复详情截图通过。 | Fixed |
| BUG-207 | 2026-10-10 | 本轮需求处理 / HTML字号 | 用户指出HTML文字本来没有问题，却被误改为大字号。 | 把项目文字太小误判为HTML设计问题，并把“不打折扣”当成像素对照。 | 撤回HTML字号、浮层尺寸和缩放修改，四档浏览器核对原12px正文／20px标题／380px浮层；保留头像、属性色和灼伤隐藏，项目修复实际可读性及基本反馈。 | Fixed |
| BUG-208 | 2026-10-10 | MatchTheme.Accent / 插画按钮 / 卡牌焦点 | 主要按钮悬停和按下样式相同，插画挡住默认反馈，键盘卡牌缺少上浮。 | Accent三态共用同一纹理；反馈仅在底层StyleBox；卡牌上浮只监听鼠标。 | 区分三态，上层反馈覆盖动态菜单／操作／遭遇／技能／英雄按钮；卡牌焦点共享上浮发光。实际鼠标渲染差异、移出恢复、Space菜单及购买／领奖／出售键盘检查通过。 | Fixed |
| BUG-209 | 2026-10-10 | CardItemView / SelectionOutline布局 | 原生验证出现大量锚点尺寸覆盖警告。 | FullRect锚点轮廓又显式设置Size，跟随卡面时发生冲突。 | 改用TopLeft锚点，LayoutFace统一定位到实际卡面；回归及原生输入重跑通过，锚点覆盖警告消失。 | Fixed |
| BUG-210 | 2026-10-10 | ButtonFeedback / Godot枚举 | 首次构建反馈层失败。 | 误用DrawModeEnum，当前SDK类型名为BaseButton.DrawMode。 | 查阅本地SDK并修正枚举名，构建0警告0错误。 | Fixed |
| BUG-211 | 2026-10-10 | 原生反馈截图检查 / 等待时机 | 遭遇按钮移出后的恢复检查失败。 | 仅等待8帧，不能保证120ms淡出结束。 | 等待180ms实际时间再取帧，正常／悬停／按下图像有差异，移出释放后恢复到正常；三类真实按钮检查通过。 | Fixed |
| BUG-212 | 2026-10-10 | CardDetailsContent / 字号放大后的详情刷新 | 全量回归245/246，展开详情在同页刷新时向上跳。 | 换行标题保留窄宽度下的旧高度，容器延迟排版；展开区也延迟扣减正文预算，刷新将暂时超高的详情强制上移。 | 给标签／标题明确可用宽度，立即排序标签并计算标题行数；展开／Render立即重算共用高度。246/246、原生三档详情及窗口检查通过，位置与展开状态保持。 | Fixed |
| BUG-213 | 2026-10-10 | CardDetailsContent / 容器通知 | 首次加入即时排版通知后构建失败。 | SDK通知常量为long，Notification参数要求int。 | 显式转换通知常量，构建0警告0错误并通过246项回归。 | Fixed |
| BUG-214 | 2026-10-10 | UI_SYSTEM.md / 配色规范 | 同一文档同时保留现行属性色表与旧关键词色表，数值着色说明也冲突。 | 属性色迁移后遗漏旧表同步。 | 现行色值统一维护于属性配色表，删除旧冲突表，说明完整再生术语及关联数值规则。 | Fixed |
| BUG-215 | 2026-10-10 | HtmlParityCapture / 两主题配色样例 | 初次蓝色主题截图恢复成原卡说明，无法比较同一内容。 | 样例只传给详情面板，主题刷新从冻结页面快照取回同ID原卡。 | 样例同步到只读页面快照，重新捕获同一十种属性说明的两主题截图并检查。 | Fixed |
| BUG-216 | 2026-10-10 | 本轮文档更新脚本 | 初次文档更新脚本未执行。 | 临时脚本中出现无效索引赋值语法，解析阶段中止。 | 删除无效语句后重新执行；首次失败未写入文件，文档及差异检查完成。 | Fixed |
| BUG-217 | 2026-10-10 | MatchTheme / 技能与图鉴详情字号 | 卡牌详情放大后，技能、图鉴与其他说明仍明显偏小。 | RichTextLabel没有独立字号，继承14px默认字体；普通悬停提示仍12px。 | 统一富文本四种字重为18px、普通提示14px，保持已有局部字号及滚动；两档原生技能／卡牌图鉴截图、长说明滚到底及246/246回归通过。 | Fixed |
| BUG-218 | 2026-10-10 | SkillItemView / 技能列表选中状态 | 技能列表出现突兀白底，选中和未选中难以区分。 | SetSelected两种状态都使用同一白色填充，等级框又遮弱边缘差异。 | 未选中恢复深色底，选中使用主题高亮底并保留等级边框；技能图鉴实际悬停／按下／松开恢复、两档页面及246/246回归通过。 | Fixed |
| BUG-219 | 2026-10-10 | HeroDetailsView / 技能、套装、奖励浮层键盘焦点 | 打开浮层后焦点留在背景按钮，Tab也可能离开浮层。 | ShowSection未转移焦点，关闭未还原入口，没有浮层内Tab边界。 | 打开聚焦关闭按钮，焦点位于浮层时Tab保持在浮层内；关闭恢复仍有效的原入口，不抢其他弹窗焦点。原生Tab／Enter关闭返回及246/246回归通过。 | Fixed |
| BUG-220 | 2026-10-10 | MatchPresenter / ButtonFeedback / 详情Escape关闭 | Escape关闭商品详情后偶发重新打开。 | 原生1920窗口重复检查第10次复现：无选中项也执行取消刷新，商品节点重建；反馈层写光标触发EnterHover重新打开详情，调用栈来自ButtonFeedback._Process。 | 无选中项不重绘；反馈层仅在光标类型变化时更新。三档各12次关闭／重新查看及商品节点保持、购买／领取／出售、真实拖拽取消与出售、246/246回归通过；构建0警告0错误。日志output/ui-html-parity/escape-*.log。 | Fixed |
| BUG-221 | 2026-10-10 | CardInteractionChecks / 临时关闭诊断构建 | 增加重新打开调用栈诊断后构建失败。 | Environment同时来自Godot与System，名称有歧义。 | 明确使用System.Environment；重建0警告0错误通过，诊断成功捕获实际重新打开调用栈。 | Fixed |
| BUG-222 | 2026-10-10 | MatchTheme / 图鉴 / 选角配色 | 星辉蓝配色下普通按钮、搜索框与部分选中底色仍为森林绿。 | 通用控件使用固定绿色，图鉴搜索框又覆盖固定绿色；技能已选条目切换主题时未重新投影。 | 按现行蓝色桌面色值同步控件状态，图鉴搜索框使用共享主题，技能选中样式刷新但保留筛选、等级和焦点；两档图鉴切换及恢复、两主题按钮真实反馈和246/246回归通过。 | Fixed |
| BUG-223 | 2026-10-10 | MentorChecks.Capture / 截图缩放基准 | 导师1280截图中备战区被窗口底部裁切，未呈现正式试玩的等比画布。 | 旧截图夹具把逻辑视口改为实际窗口尺寸，偏离1600×900基准。 | 捕获保持1600×900逻辑视口，只调整实际窗口；1280／1920捕获及完整备战区边界检查通过，1280截图已复查。 | Fixed |
| BUG-224 | 2026-10-10 | CardInteractionChecks / 原生出售回归 | 两主题反馈检查通过后，Enter出售确认断言失败。 | 原日志未区分金币、库存或详情重新打开，尚未确认根因。 | 增加详情身份、实际／期望金币、原卡是否存在及焦点诊断；随后原生回归通过，未作为修复证据。用户要求停止扩展细节检查，保留记录待复现。 | Open |
