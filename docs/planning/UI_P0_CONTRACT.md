# UI P0：职责、状态与展示契约

日期：2026-09-30。源码基线：`MinimalPlaytest.cs`、`MatchSnapshot.cs`、`GameCoordinator.cs`。本文是 P1/P2 的迁移依据，尚未改变运行界面。

沿用 [UI 系统设计](../design/UI_SYSTEM.md) 的三行三列布局与 [UI 实施计划](UI_IMPLEMENTATION_PLAN.md) 的阶段边界。契约中的类型名表示后续实际职责，不在 P0 创建空类或通用框架。

## 1. 方法迁移表

| 现有方法 | 迁移所有者 | 保留或拆分职责 |
|---|---|---|
| `_Ready` | `MinimalPlaytest` | 服务组装、资源初始化、取得子视图、连接事件；增加 `_ExitTree` 解除订阅 |
| `ShowHeroSelection` | `MatchPresenter` + `HeroSelectionView` | Presenter 清理对局上下文并进入选角；视图只 Render |
| `SelectHero`、`FirstHero` | `MatchPresenter` + 应用只读查询 | 以英雄 `StringName` key 提交选择；查询输出稳定排序的候选；保留当前测试起始余额口径 |
| `ShowEncounterChoices` | `MatchPresenter` | 显式调用生成遭遇用例一次；随后刷新；Render 不调用生成 |
| `ChooseEncounter` | `MatchPresenter` | 检查当前页面与候选 key，调用选择用例，成功后进入对应页面 |
| `ShowShop`、`RefreshShop` | `MatchPresenter` | 首次进入创建访问上下文；刷新调用应用服务；重新 Render 不重抽 |
| `BuyCard` | `MatchPresenter` + 经济应用用例 | Presenter 提交购买意图；购买/合并/放置完整事务在 P4 由应用层承接，不移到子视图 |
| `RefreshShopButtons` | 展示适配器 + `ShopView` | 只读商品数据转 ViewModel；视图显示购买与刷新状态 |
| `ShowEvent` | `MatchPresenter` + `EventView` | 创建选项上下文一次；视图按条目数生成按钮 |
| `ResolveEventOption` | `MatchPresenter` + 展示适配器 | 调用事件用例；格式化 Result；成功后统一刷新卡牌与 HUD |
| `ShowPreparation` | `MatchPresenter` + `OpponentBoardView` | 本地对手创建一次；战斗类型/轮次保存在操作上下文；视图只读显示 |
| `BuildBoardSlots`、`CreateBoardFace` | `BoardZoneView`、`CardItemView` | P2 接管节点创建与输入；P3 按完整占格排版，取消固定缩放 |
| `OnBoardSlot` | `BoardZoneView` + `MatchPresenter` | 视图发出实体/区域/格坐标；Presenter 管理选择并调用 BoardService |
| `RefreshBoard`、`RefreshEnemyBattlefield`、`RefreshZone` | 展示适配器 + `BoardZoneView` | 从同一批快照生成三条轨道，视图不再自行读取 Session |
| `SetBoardFace` | `CardItemView` | 卡面 Render 与布局分开；输入范围覆盖全部占格 |
| `CreateCardFaceViewModel` | 卡面展示适配器 + 视觉资源映射 | 应用查询提供当前效果；表现层格式化并按完整 key 加载/缓存纹理 |
| `FindAt` | `BoardZoneView` | 在只读放置列表中命中；不实现推挤规则 |
| 三个 `FormatCardFace` 重载、`KindName` | 展示适配器 | 身份与语义格式化；同一数据用于商品/棋盘/详情，迁移后删除旧重复路径 |
| `StartBattle` | `MatchPresenter` | 结算调用一次；P1 仍直接显示结果；P5 才引入完整播放输入 |
| `ClaimMonsterReward` | `MatchPresenter` + `RewardView` | 应用用例领取；失败保留待领取项；成功统一刷新 |
| `ContinueMatch` | `MatchPresenter` | 按对局状态进入下一阶段或选角，不由按钮文案决定行为 |
| `HideAllActions` | 删除；职责归页面模型与显式退出流程 | Render 决定可见性；关闭访问上下文仅在成功离开、重开、卸载时进行 |
| `UpdateState` | `RefreshView` + 顶栏/英雄/成长视图 | 同次读取，一次生成完整模型；已占格与卡牌数量分开 |
| `FormatBattleLog` | 战斗结果展示适配器 | 使用既有事件格式化日志；不能用日志补算缺失 HUD |

`MatchPresenter` 独占 `_player`、`_enemy`、页面、战斗类型/轮次、当前商店/事件访问句柄。可变 Session 和访问对象只传给应用服务，不进入 Render 接口。业务数据仍由应用/领域对象拥有，Presenter 不复制一份可修改的卡牌或资源集合。

## 2. 页面与允许操作

| 页面 | 上排中央 | 内容操作 | 右上操作 | 我方双棋盘 |
|---|---|---|---|---|
| HeroSelection | 英雄候选 | 选择英雄并确认 | 隐藏 | 隐藏 |
| EncounterChoice | 实际遭遇候选 | 选择遭遇 | 隐藏，不跳过回合 | 可移动/查看 |
| Shop | 商品与报价 | 购买、刷新 | 离开商店 | 可移动/查看；P4 增加出售 |
| Event（未完成） | 动态选项 | 执行选项 | 隐藏 | 可移动/查看 |
| Event（已完成/无选项） | 结果摘要 | 查看结果 | 继续旅程 | 可移动/查看 |
| Preparation | 敌方战场 | 开始战斗 | 隐藏，不增加逃离规则 | 可移动/查看 |
| Playback（P5） | 冻结敌方阵容 | 暂停、倍速、跳过，具体方案待 P5 确认 | 禁用对局离开 | 只读，备战不参战 |
| BattleResult | 本场结果与资源差额 | 查看/领取允许的奖励 | 进入下一回合 | 对局进行中时可移动/查看 |
| MatchEnded | 对局摘要 | 查看允许的末局奖励 | 返回英雄选择 | 只读；领取按应用用例允许性决定 |

奖励、技能、套装是浮层内容，不增加独立对局流程状态。任务放卡牌详情。按用户继续指令采用左下英雄信息区的技能/套装/奖励入口，任务放卡牌详情；线框是尺寸草稿，视觉验收在后续阶段执行。

所有操作先检查页面、上下文和执行中状态，再调用用例；按钮禁用不能代替服务校验。失败留在当前页面。选择遭遇后才创建对应访问上下文；事件完成标记是 Presenter 已成功执行选项的页面事实，不复制领域结果。MatchStatus 终局优先进入 MatchEnded。

P1 保留现有 `ResolveBattle` 一次计算并结算、直接进入结果页的行为；不得为等待播放重复调用。播放页的加入及冻结数据包在 P5 完成后才启用。

## 3. UiState 与生命周期

| 状态 | 唯一所有者 | 清理时机 |
|---|---|---|
| SelectedCardId、HoveredCardId | Presenter 持有的 `UiState` | 实体失效、换页、重开、卸载；失败移动保留有效选择 |
| DragSourceId、源区域/抓取偏移、目标区域/格、只读预览 | `UiState` | 松手提交、取消、换页、Resize 后预览失效、实体移除、卸载 |
| 详情目标（实体或商品定位）、固定展开状态、二级浮层 | `UiState` | 目标失效、访问改变、换页、重开、卸载；普通同页刷新保持有效目标 |
| 命令执行中与当前反馈 | `MatchPresenter` | 用例完成后释放门控；换页清过期反馈；卸载断开后续 Render |
| 延迟悬停计时、焦点、Control 节点 | 各视图 | 视图隐藏/销毁时取消计时；仅渲染态，不保存业务实体 |
| 播放游标/速度/动画 | P5 `BattlePlaybackPresenter` | 播放退出、重开、卸载 |

重开先清商店、事件、敌方、战斗结果、UiState，再进入选角；新局从应用入口创建。商店离开后清当前访问，待领奖励仍由 Session 保留。场景切换取消拖拽和提示，解除所有订阅。

## 4. RefreshView 输入输出

入口由 Presenter 拥有：显式业务操作结束 → 更新合法页面/上下文 → `RefreshView` → `MatchPageViewModel` → `MatchShell.Render`。失败也刷新操作条件并保留反馈。

输入是当前页面、玩家/敌方访问句柄、商店/事件上下文、最近 Result 摘要与 UiState。应用查询在同一同步刷新中捕获玩家和当前上下文；捕获期间不穿插任何业务写入。适配器只接收捕获后的只读结果。

输出 `MatchPageViewModel` 包含页面、顶栏、场景头像、当前内容、右上动作、我方战场、备战、英雄、成长信息、浮层与反馈；每项动作携带是否可用及原因。HeroSelection 无 Session 时输出候选及隐藏棋盘，不保留上一局模型。

Render 不扫描注册表、不创建 Session、不创建对手、不生成选项、不消费随机数、不发放收入或奖励。Resize 使用已存模型重排；悬停与选择可复用最近业务快照更新表现。无需通用命令总线、全局事件总线或逐帧查询。

| 只读契约 | 已有字段 | P1/P4/P5 待补字段与来源 |
|---|---|---|
| `MatchSnapshot` / HUD | MatchId、Status、Round/Turn、Wealth/Income、Experience/Reputation、PvpWins、Summary | P1 英雄身份、英雄等级及构筑属性；应用层读英雄属性集，UI 不推导升级规则 |
| `CardSnapshot` / 卡面详情 | Id、Key、DisplayName、Level、Value、Size、FactionKey、ElementKeys、Quests | P1 标签、SetKey、等级基础值与实例当前值、能力效果/条件、冷却/魔法需求；应用查询求值，表现层通用格式化 |
| `BoardZoneSnapshot` | BoardPlacementSnapshot 的 CardId、Zone、Start/EndExclusive；卡牌张数 | P1 容量、已占格；P3 只读预览计划与原因；用例重新校验提交，不信任旧预览 |
| `ShopViewSnapshot` | ShopStock/Offer 中报价、等级、售罄、RefreshCost、CanRefresh | P1 复制成只读数据；当前访问和报价批次定位、合并候选/空间/余额条件；P4 完整事务校验过期操作 |
| `EventViewSnapshot` | EncounterOptionSet、选项 key/名称、执行 Result | P1 只读成本/已知收益/可用原因/完成摘要；不提前抽未知奖励；定位当前事件访问 |
| 技能/套装/任务 | SkillSnapshot 身份/等级；QuestProgressSnapshot 进度/次数/解锁 | P1 技能能力摘要、套装身份/去重数量/各阈值状态；复用套装求值器，不在 UI 重算效果 |
| `RewardViewSnapshot` | PendingMonsterRewards 的待领取信息 | P4 独立只读复制、卡牌/技能展示与可领取原因；满盘失败保留 |
| 战斗结果/播放输入 | BattleResult、Events、EndedAt、Outcome、结算后 MatchSnapshot | P5 冻结战前英雄 HUD/卡牌/位置及可定位状态变化；暂不把现有日志当成完整回放输入 |

所有标识 key 使用 `StringName`；实例用 `EntityId`；展示文本用 `string`。商品批次和访问仅用于区分当前操作上下文，不增加玩法身份字段。集合必须复制，不能把可变对象仅包装成 IReadOnlyList 后交给视图。当前 CardSnapshot.Value 取 BaseValue；P1 必须核对出售用例口径再提供显示值，不能直接假定它等于含加成的当前出售价值。

## 5. 尺寸预算与三页排版稿

以下是可审查的线框预算，尚未经过 Godot 截图验收。三页仅替换上排内容，我方两条轨道保持位置；中排左右不放功能。白金/天蓝主题和具体卡面排版在 P6 校准。

| 项目 | 1920×1080 | 1280×720 |
|---|---|---|
| 页面外边距 / 列间距 / 行间距 | 24 / 16 / 16 | 16 / 12 / 12 |
| 顶栏（x,y,w,h） | 24,24,1872,216 | 16,16,1248,144 |
| 左/中/右列宽 | 312 / 1216 / 312 | 208 / 808 / 208 |
| 三行 y / 高 | 256、528、800 / 256 | 172、352、532 / 172 |
| 中央列 x | 352 | 236 |
| 行内左右留白 / 标题操作带 / 底部留白 | 16 / 40 / 16 | 12 / 40 / 12 |
| 共用轨道格宽 u / 卡高 2u | 100 / 200 | 60 / 120 |
| 十格轨道宽 / x | 1000 / 460 | 600 / 340 |
| 三条轨道 y | 296、568、840 | 212、392、572 |

格宽取各轨道可用尺寸的最小值，并受设计起点 100 限制；小/中/大型宽度分别 u/2u/3u。1280 方案卡名仍按最低 14px 独立排版，不能整体缩放文字；宽 60 的长名称省略并在详情完整显示。上排 40px 操作带容纳区标题与开始/刷新按钮；候选和商品数量较多时在上排内部横向滚动，不拉宽三列。

- [构筑/遭遇线框](../design/ui-p0/encounter.svg)：上排实际候选，左上通用占位，右上不跳过回合。
- [商店线框](../design/ui-p0/shop.svg)：商品卡面与独立报价，内容内购买/刷新，右上离开商店。
- [战斗准备线框](../design/ui-p0/preparation.svg)：敌方头像与十格轨道，开始按钮在内容内，右上无逃离操作。

三张图均显示两个目标分辨率和共同格线；左下二级入口位置已按用户继续指令采用，功能将在P2/P4接入。

## 6. 复现与验证基线

保留 `MANUAL_ACCEPTANCE.md` 的 2026-09-29 规则 97/97 通过及用户试玩闭环验收记录。以下为待执行的复现步骤，P0 仅核对源码，不把问题改为 Fixed。

| BUG | 当前源码位置 | 人工复现与后续通过标准 |
|---|---|---|
| 028 | `MinimalPlaytest.cs`：CreateBoardFace / RefreshZone / SetBoardFace | 购买小/中/大型卡，观察名称和完整占格；切 1920/1280 并 Resize。后续应完整覆盖 1/2/3 格、卡名可读、命中与绘制一致，无后续方块按钮 |
| 029 | 同文件：CreateCardFaceViewModel | 查看臂铠、野猪、审判之锤；需匹配三张已有原画；无图卡也能正常查看。正式 key 必须包含 card. 前缀 |
| 030 | 同文件：CreateCardFaceViewModel | 查看修女治疗、审判之锤百分比效果；通过校场/铁匠铺强化后查看卡面与详情。应显示治疗/百分比语义及实例加成，而不是只显示等级基础值 |
| 031 | 同文件：ResolveEventOption / UpdateState | 垃圾场获得卡牌或校场强化后，不移动、不换页，立即查看棋盘/详情/HUD。后续应同次刷新，无需额外点击 |
| 032 | 同文件：ChooseEncounter / ShowPreparation / HideAllActions | 第4回合选择怪物、第8回合进入PvP，检查敌方区域。后续数据和可见性必须同步；当前源码核对，场景复现未执行 |

商店与遭遇依随机候选出现时可推进多轮等待对应内容；不为复现修改正式内容权重。P1 修复后执行对应场景与相关规则检查，P3 再验收 028 的布局与交互。
