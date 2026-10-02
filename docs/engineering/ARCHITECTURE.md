# 架构说明

玩法以 [游戏规则](../design/GAME_DESIGN.md) 为准，标识与身份等硬约束见 [AGENTS](../../AGENTS.md)，界面契约见 [UI 规范](../design/UI_SYSTEM.md)。本文只描述实现边界；未实现事项见 [当前计划](../planning/IMPLEMENTATION_PLAN.md)。

## 分层与依赖

| 层 | 职责 |
|---|---|
| Domain | 普通 C# 的定义、实体属性、对局/棋盘模型、决策与确定性战斗；不依赖 Node、场景树或 UI |
| Application | 创建对局、经济、棋盘、遭遇、奖励和战斗结算用例；协调完整事务，返回 Result 与 Snapshot |
| Infrastructure | 定义反射扫描/校验、确定性随机和外部适配实现 |
| Presentation | 服务组装、输入、只读 ViewModel、资源加载与 Godot 生命周期 |
| Content | 独立的英雄、卡牌、技能、遭遇、怪物与套装定义，组合通用能力 |

所有代码和对应 `.cs.uid` 位于 `scripts/` 下的五层目录，层内按业务职责组织。Application 的内容访问依赖领域只读 `IDefinitionCatalog`；`DefinitionRegistry` 实现该契约并负责扫描与校验。具体基础设施在入口组装，UI 流程接收对手来源接口。当前 SeededRandom 是应用与战斗共用的确定性实现，保持显式 seed/状态；其他外部适配在出现实际替换需求时再引入契约。

## 生命周期与状态所有权

- 全局：不可变内容目录、实体工厂与应用服务，不保存本局玩家资源。
- 对局：`MatchSession` 聚合根拥有玩家、卡牌/技能实例、双棋盘、进度、奖励、遭遇历史、随机状态与对局事件。新局整体重建。
- 战斗：`BattleSetup` 是冻结输入；`BattleRuntime` 只拥有本场临时状态；`BattleResult` 返回结果、永久变化、事件和播放状态，应用层负责回写。
- 表现：只拥有页面、访问句柄、交互与播放状态；控件接收只读快照。

定义、实例与运行态分开。Definition 不可变，EntityFactory 创建独立 EntityId 与属性集，实例之间不共享可变属性。身份在属性集只读分区，Persistent 保存对局内可变属性，BaseCombat 保存战斗基础值；战斗不直接写实例。

## 内容与扩展入口

DefinitionRegistry 扫描公开、非抽象、有无参构造的 Definition；验证 key 唯一、归属、套装、怪物卡组/技能、棋盘布局和遭遇选项引用。内部验证定义不进入正式池。注册不是实例池，不租借或复用实例。

| 扩展需求 | 入口 |
|---|---|
| 新英雄/卡牌/技能/怪物/遭遇/套装 | Content 下独立 Definition + 正式 CSV + 验证；不增加中央内容注册 switch |
| 新卡牌行为 | 组合通用 AbilityDefinition / EffectDefinition；组合无法表达时才添加可复用机制 |
| 新战斗效果 | BattleEffectResolver 与通用效果数据，更新行为和顺序验证 |
| 新对手来源 | 实现 IOpponentProvider，在试玩入口注入；流程不创建具体 Provider |
| 新玩家操作 | 应用用例 + Result + 必要查询，不让 UI 写模型 |
| 新展示 | Snapshot / ViewModel / Adapter，领域对象不加载贴图或场景 |

只为已确认需求建立抽象；出现实际复用或明确替换点时抽取。接口保持小而聚焦，不建立万能 Context、Manager、服务定位器或全局命令总线。Command 表示意图，Result 表示同步成功/失败，Domain Event 表示已发生事实；有返回值的操作不伪装成事件。Match Event 与 Combat Event 强类型隔离，不跨总线传播。

CSV 与 Definition 的维护和一致性验证见 [内容数据规范](../design/CONTENT_DATA.md)。展示描述不参与规则解析。

## 对局用例与事务

- GameCoordinator 协调创建、遭遇、战斗与结果；经济、棋盘、事件与奖励有明确应用入口。
- BoardPlacementSolver 是纯函数，评估直接放置/左右推挤，返回完整计划。优先级为总距离短 → 影响卡牌少 → Right；评估前释放移动卡原位。
- BoardService 验证归属，成功后一次提交同盘/跨区位移；预览不修改状态，提交重新计算。
- CardMergeDecision 稳定选择合并目标，CardEconomyService 提交创建或升级。BuyAndPlace / AcquireAndPlace 预检金额、售罄、合并与完整放置，成功后扣款/标记；SellFromBoard 预检价值、溢出与奖励依赖后统一出售。
- RoundIncomeService 在生成新轮遭遇前结算，并按轮次保证幂等；第一轮在创建对局后结算。
- EncounterScheduler 稳定过滤/抽取候选，保存候选出现历史与随机状态。MonsterDefinition 自动投影为怪物遭遇；试玩允许内容不足三个，正式排程保持严格。
- ResolveEncounterOptionService 创建本次固定/加权选项，一次性选择成功后提交通用效果。MonsterRewardClaimService 只在领取成功后移除奖励。

失败不得留下扣款但没有卡牌、源棋盘移除但目标未加入等部分状态。同一实例只位于一个区域，棋盘实例必须属于本局卡池。外部读取用复制的 Snapshot / ViewModel，不暴露可变集合给 UI。

## 属性、套装与任务

FinalValue = BaseValue + Sum(Modifiers)。Modifier 有唯一 ID、来源、目标属性与贡献；Apply 累加，移除按 ID 精确扣除。基础值可依赖自身等级、标签、位置、区域和符合条件的卡牌数量，不读取其他卡牌的最终值。战斗开始后使用冻结基础值，变化只在 Runtime。

元素为规范顺序的 1～2 个不重复 StringName；General 是真实普通属性，不作空值哨兵。元素参与分类/筛选/条件，不计算克制。标签参与规则，词条是能力行为的文案语义。

CardSetEvaluator 只按战场不同 CardKey 计数，并稳定输出达到的全部阈值；阈值解锁通用能力。CardSetBonusService 在棋盘变化时精确重算持续 Modifier，战斗能力由 BattleSetupFactory 冻结。战斗内摧毁不重新求值套装快照。

CardQuestService 按对局事件推进当前拥有卡牌的实例任务，包括备战区。CardQuestBonusService 只为战场已解锁卡牌施加持续属性能力；移区/移除时精确撤销。其他解锁能力加入来源卡的战斗快照，随该来源被摧毁失效。

## 战斗模块与固定顺序

| 协作者 | 职责 |
|---|---|
| CombatSimulator | 创建 Runtime、推进 Tick、FIFO 入队/发动、魔法与冷却门控、回响截断及终止判定 |
| BattleEffectResolver | 通用效果执行、伤害、卡牌属性/光环查询、充能与摧毁；只操作 Runtime |
| BattleStatusResolver | 卡牌冷却与时长衰减、状态施加、相邻状态反应、英雄周期状态与日蚀 |
| BattleStateRecorder | 冻结事件前缀对应的状态，生成结算结果；不推进随机或规则 |

入口只接受 BattleSetup。所有协作者在 Domain/Combat 内部，不向 UI 暴露战斗写入口。卡牌/技能/套装共用 IBattleAbilitySource、PendingAbility 和效果解析，不复制独立模拟器。

初始化记录状态和 BattleStarted，然后结算 0 Tick 战斗开始被动并检查死亡。循环在 Tick 小于 Timeout 时进入下一 Tick，依次执行：日蚀 → 冷却/卡牌时长递减 → 主动入队 → 双方英雄状态 → 状态记录 → FIFO 能力结算 → 死亡检查。循环到期后双方生命置零并按寂灭规则生成结果。整数 Tick 与稳定来源顺序、FIFO、显式 seed 共同保证复算一致；不使用渲染帧决定结果。

主动发动先验证来源和魔法，再扣魔法、重置冷却、发布发动事件并执行效果。多重从当前卡牌属性读取，额外发动不递归生成多重、不重复付魔法。光环按当前 Runtime 求值，来源离开战场或被摧毁即失效；技能来源不依赖棋盘。回响执行产生的事件不再次触发回响。每场首次己方卡牌发动触发由能力状态保证至多一次。

Runtime 只修改卡牌实际支持的属性，修改效果不能凭写属性赋予新能力。每次真实变化点记录冻结状态；同 Tick 保持原记录顺序，EventCount 对应原日志前缀。BattleResult 包含永久变化与只读播放状态，GameCoordinator.ResolveBattleForPlayback 通过 MatchResultService.Apply 一次结算，不因播放方式重跑。UI 回放细节见 UI 规范。

## 验证与审查

Main.tscn 保留分类/全部验证及 headless --verify。Presentation/Verification 中按领域职责划分规则检查和共享夹具，PhaseOneVerification 只组装列表和显示结果；场景集成仍在 Godot 中验证。

重点覆盖不可变定义与实例隔离、CSV 一致性、Modifier 精确堆叠/移除、经济失败原子性、棋盘推挤与回滚、遭遇随机/历史、FIFO/回响、状态周期、日蚀/寂灭、永久变化与确定性回放。架构迁移保留既有验证，并比较固定输入的结果、事件顺序和冻结状态。

审查时检查状态所有者、Match/Battle 生命周期、事务失败路径、随机/时间/遍历顺序、跨层写入、能力复用、快照隔离和实际扩展收益。BUG 登记及提交约定见 AGENTS 与开发约定。
