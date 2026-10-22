# 架构说明

玩法以 [游戏规则](../design/GAME_DESIGN.md) 为准，标识与身份等硬约束见 [AGENTS](../../AGENTS.md)，界面契约见 [UI 规范](../design/UI_SYSTEM.md)。本文只描述实现边界；未实现事项见 [当前计划](../planning/IMPLEMENTATION_PLAN.md)。

## 分层与依赖

| 层 | 职责 |
|---|---|
| Domain | 普通 C# 的定义、实体属性、对局/棋盘模型、决策与确定性战斗；不依赖 Node、场景树或 UI |
| Application | 创建对局、经济、棋盘、遭遇、奖励和战斗结算用例；协调完整事务，返回 Result 与 Snapshot |
| Infrastructure | 定义反射扫描/校验、确定性随机和外部适配实现 |
| Presentation | 服务组装、输入、只读 ViewModel、资源加载与 Godot 生命周期 |
| Content | 独立的英雄、卡牌、技能、遭遇、怪物、导师与套装定义，组合通用能力 |

所有代码和对应 `.cs.uid` 位于 `scripts/` 下的五层目录，层内按业务职责组织。Application 的内容访问依赖领域只读 `IDefinitionCatalog`；`DefinitionRegistry` 实现该契约并负责扫描与校验。具体基础设施在入口组装，UI 流程接收对手来源接口。当前 SeededRandom 是应用与战斗共用的确定性实现，保持显式 seed/状态；其他外部适配在出现实际替换需求时再引入契约。

## 生命周期与状态所有权

- 全局：不可变内容目录、实体工厂与应用服务，不保存本局玩家资源。
- 对局：`MatchSession` 聚合根拥有玩家、卡牌/技能实例、双棋盘、进度、奖励、遭遇历史、随机状态与对局事件。新局整体重建。
- 战斗：`BattleSetup` 是冻结输入；`BattleRuntime` 只拥有本场临时状态；`BattleResult` 返回结果、永久变化、事件和播放状态，应用层负责回写。
- 表现：只拥有页面、访问句柄、交互与播放状态；控件接收只读快照。

定义、实例与运行态分开。Definition 不可变，EntityFactory 创建独立 EntityId 与属性集，实例之间不共享可变属性。身份在属性集只读分区，Persistent 保存对局内可变属性，BaseCombat 保存战斗基础值；战斗不直接写实例。

宝石孔数位于卡牌只读身份，镶嵌结果由卡牌实例独立保存。`GemShopService` 渠道用例负责报价、费用和访问授权，`MatchSession.ActiveGemShopVisit` 保存当前砧板、版本、刷新及完成状态，再通过 `SocketGemService` / `SocketGemCommand` 校验对局、卡牌归属、发动及空孔并提交；没有独立宝石库存。合并保留原目标实例，材料实例移除时连同其宝石消失。`CardAbilityComposer` 将宝石效果附加到原发动，工厂冻结战斗输入；战斗层在原效果执行前冻结主值。UI读取 `CardSnapshot.GemSockets` 及砧板ViewModel副本，不写孔位；预览不扣费或消耗随机数，离店关闭访问，旧版本提交无效。

## 内容与扩展入口

DefinitionRegistry 扫描公开、非抽象、有无参构造的 Definition；验证 key 唯一、归属、套装、怪物卡组/技能、棋盘布局和遭遇选项引用。内部验证定义不进入正式池。注册不是实例池，不租借或复用实例。

| 扩展需求 | 入口 |
|---|---|
| 新英雄/卡牌/技能/怪物/导师/遭遇/套装 | Content 下独立 Definition + 验证；不增加中央内容注册 switch |
| 新卡牌行为 | 组合通用 AbilityDefinition / EffectDefinition；组合无法表达时才添加可复用机制 |
| 新战斗效果 | BattleEffectResolver 与通用效果数据，更新行为和顺序验证 |
| 新对手来源 | 实现 IOpponentProvider，在试玩入口注入；流程不创建具体 Provider |
| 新玩家操作 | 应用用例 + Result + 必要查询，不让 UI 写模型 |
| 新展示 | Snapshot / ViewModel / Adapter，领域对象不加载贴图或场景 |

可选项结算中的加权结果由应用用例使用对局随机状态抽取，成功后提交随机状态；同等级怪物筛选返回只读 `EncounterChoice`。表现层使用既有对手来源进入战斗准备、回放及奖励结算，不再次调用遭遇选择来推进回合。访问等级与轮次由选项集合捕获，怪物轮次范围保存在不可变定义中。

只为已确认需求建立抽象；出现实际复用或明确替换点时抽取。接口保持小而聚焦，不建立万能 Context、Manager、服务定位器或全局命令总线。Command 表示意图，Result 表示同步成功/失败，Domain Event 表示已发生事实；有返回值的操作不伪装成事件。Match Event 与 Combat Event 强类型隔离，不跨总线传播。

Definition 的维护和行为验证见 [内容数据规范](../design/CONTENT_DATA.md)。卡牌展示描述保存为只读有序 `CardDescriptionEntry`（StringName KeywordKey + string Text），随身份传入快照；UI按条目渲染，不参与规则解析或能力生成。

技能图标原画资源标识位于`SkillIdentityAttributes.Illustration`，随实例与`SkillSnapshot`冻结传递；领域层不加载纹理。局外`SkillCatalogQuery`从内容目录投影全部支持等级，表现层只持有条目与快照；浏览不创建实体、获得技能、触发能力或推进随机。

## 对局用例与事务

- GameCoordinator 协调创建、遭遇、战斗与结果；经济、棋盘、事件与奖励有明确应用入口。
- BoardPlacementSolver 是纯函数，评估直接放置/左右推挤，返回完整计划。优先级为总距离短 → 影响卡牌少 → Right；评估前释放移动卡原位。
- BoardService 验证归属，成功后一次提交同盘/跨区位移；预览不修改状态，提交重新计算。
- MergeDecision 按同等级候选的稳定ID选择，CardEconomyService 构建连续合并链，每一步保留原有更高等级卡，最终提交最高等级目标的升级并删除低等级材料。BuyAndPlace / AcquireAndPlace 预检金额、售罄、合并与完整放置，成功后扣款/标记；SellFromBoard 预检价值、溢出与奖励依赖后统一出售。
- 合成价值在应用经济用例补新旧等级标准获得价值差额，保留最终目标已有成长与Modifier，不吸收材料价值；获得、购买及棋盘交易入口在移除材料前检查最终价值与基础值溢出。
- RoundIncomeService 在生成新轮遭遇前结算，并按轮次保证幂等；第一轮在创建对局后结算。
- EncounterScheduler 稳定过滤/抽取候选，保存候选出现历史与随机状态。MonsterDefinition 自动投影为怪物遭遇；试玩允许内容不足三个，正式排程保持严格。
- ResolveEncounterOptionService 创建本次固定/加权选项，一次性选择成功后提交通用效果。MonsterRewardClaimService 只在领取成功后移除奖励。
- MentorService 独立开启导师访问，复用 SkillAcquisitionService 领取与合并。MentorDefinition 只保存身份、默认等级和纯筛选入口；MentorEncounterDefinition 显式引用导师，排程不自动将导师投影为遭遇。遭遇候选等级传入访问，独立访问使用导师默认等级。
- MatchSession 拥有 ActiveMentorVisit 及其固定只读候选、等级和完成状态；失败不关闭选择，不重抽。领取校验当前对局持有的访问引用，拒绝跨局、旧访问和重复领取；MatchSnapshot 冻结导师访问，Presenter 仅持有访问句柄，视图不接收可变访问对象。

失败不得留下扣款但没有卡牌、源棋盘移除但目标未加入等部分状态。同一实例只位于一个区域，棋盘实例必须属于本局卡池。外部读取用复制的 Snapshot / ViewModel，不暴露可变集合给 UI。

## 属性、套装与任务

FinalValue = BaseValue + Sum(Modifiers)。Modifier 有唯一 ID、来源、目标属性与贡献；Apply 累加，移除按 ID 精确扣除。基础值可依赖自身等级、标签、位置、区域和符合条件的卡牌数量，不读取其他卡牌的最终值。战斗开始后使用冻结基础值，变化只在 Runtime。

元素为规范顺序的 1～2 个不重复 StringName；General 是真实普通属性，不作空值哨兵。元素参与分类/筛选/条件，不计算克制。标签参与规则，词条是能力行为的文案语义。

卡牌实例另存十进制 `CooldownMultiplier`，生命周期同任务进度，默认1且升级保留。经济用例成功出售后提交通用目标缩减；MatchSnapshot和BattleSetupFactory复制倍率，不把对局对象传入战斗。Runtime首次冷却与重置应用冻结倍率，剩余半Tick单位改为decimal以保留小数；整数Tick推进和既有无缩减战斗顺序保持一致。

CardSetEvaluator 只按战场不同 CardKey 计数，并稳定输出达到的全部阈值；阈值解锁通用能力。CardSetBonusService 在棋盘变化时精确重算持续 Modifier，战斗能力由 BattleSetupFactory 冻结。战斗内摧毁不重新求值套装快照。

对局事件能力使用PassiveOnRoundStart／PassiveOnBattleVictory／PassiveOnMonsterVictory。RoundIncomeService沿用轮结算边界发放套装经验；BattleRuntime只冻结开战MatchRewardSource，BattleResult返回这些来源，不执行对局奖励。MatchAbilityService在应用结算读取冻结来源与等级，复用CardEconomyService获得卡牌和英雄经验；满盘奖励转待领取，同一结果的能力奖励保持幂等。按英雄等级提供的持续Modifier在棋盘与各经验结算入口重算，升级更新贡献，失效精确撤销。

CardQuestService 按对局事件推进当前拥有卡牌的实例任务，包括备战区。CardQuestBonusService 只为战场已解锁卡牌施加持续属性能力；移区/移除时精确撤销。其他解锁能力加入来源卡的战斗快照，随该来源被摧毁失效。

卡牌等级的 BattleVictoryBonuses 是可复用的对局结算配置。MatchResultService 根据 BattleResult 的开战快照筛选己方非备战区卡牌，获胜后给仍在库存的实例累加永久 Modifier；不读取战斗临时属性，不复用一次性任务解锁。EntityFactory 在升级时刷新增量并保留 Modifier。

卡牌等级的 BattleValueBonus 复用同一参战筛选，不区分战斗胜负，将永久价值贡献累加到实例 Persistent 分区。BattleSetupFactory 复制实例实际价值到只读 CombatValues，供通用属性施毒读取；战斗与回放不读取结算后的价值，EntityFactory 升级只刷新后续成长增量。

## 战斗模块与固定顺序

ElementSequenceResolver在成功主动发动支付及多重排程后消费来源能力各自的一次性元素窗口，将本次伤害倍率冻结在PendingAbility，发动完成后刷新前置窗口并将状态联动排入FIFO。BattleEffectResolver仅放大该次发动伤害，状态复用通用随机目标结算；BattleStateRecorder通过只读倍率查询冻结卡面值，不推进或消费窗口。回响与开战不进入窗口链，来源失效仍由Runtime判定。

战斗内修复复用被摧毁的CardBattleState，不创建对局卡牌或新身份。BattleEffectResolver按标签、永久摧毁记录及完整占格筛选，用冻结随机顺序选择目标，恢复光环后重置主动冷却；AbilityQueue在摧毁时取消来源旧队列，避免同Tick修复复活旧发动。CardRepairedEvent携带位置事实，回放只读取事件与快照，按召唤／修复顺序替换可见占格，不写对局棋盘。

| 协作者 | 职责 |
|---|---|
| CombatSimulator | 创建 Runtime、推进 Tick、FIFO 入队/发动、魔法与冷却门控、回响截断及终止判定 |
| BattleEffectResolver | 通用效果执行、伤害、卡牌属性/光环查询、充能与摧毁；只操作 Runtime |
| BattleStatusResolver | 卡牌冷却与时长衰减、状态施加、相邻状态反应、英雄周期状态与日蚀 |
| BattleQuestResolver | 自身发动任务进度、阈值即时解锁与通用属性/能力/身份奖励；只操作 Runtime |
| BattleStateRecorder | 冻结事件前缀对应的状态，生成结算结果；不推进随机或规则 |

Runtime 独立保存全场日蚀进入状态及实际起点；自然边界和提前进入效果共用一次性入口，在伤害前按稳定来源顺序执行进入增益。播放状态冻结日蚀布尔值与增长后的英雄生命上限，不根据原始Setup的预定时间猜测日蚀。轮开始套装冷却缩减由MatchAbilityService按当前已激活阈值结算，RoundIncomeService保持轮边界幂等。

入口只接受 BattleSetup。所有协作者在 Domain/Combat 内部，不向 UI 暴露战斗写入口。卡牌/技能/套装共用 IBattleAbilitySource、PendingAbility 和效果解析，不复制独立模拟器。

初始化记录状态和 BattleStarted，然后结算 0 Tick 战斗开始被动并检查死亡。循环在 Tick 小于 Timeout 时进入下一 Tick，依次执行：英雄隐身时长递减 → 日蚀 → 冷却/卡牌时长递减 → 到期多重入队 → 主动入队 → 双方英雄状态 → 状态记录 → FIFO 能力结算 → 死亡检查。循环到期后双方生命置零并按寂灭规则生成结果。整数 Tick 与稳定来源顺序、FIFO、显式 seed 共同保证复算一致；不使用渲染帧决定结果。

相邻召唤由战斗层使用冻结模板创建临时 Runtime 卡牌，校验实际战场容量和完整占格；对局库存与棋盘不变。ID由来源和本场召唤序号确定性派生，新增卡牌重新按既有顺序排列；召唤事件及身份、位置、初值随状态冻结，回放仅投影临时卡面和放置，不执行召唤。召唤物即使受到永久摧毁，也不产生对局库存变更。

主动发动先验证来源和魔法，再扣魔法、重置冷却、发布发动事件并执行效果。BattleCooldownRules在固定增减与有效倍率之后应用完整冷却一秒下限，初始化、重置、动态光环、修复、快照和预览共用计算。多重在首次正常发动时读取次数，Runtime按每2 Tick排程额外发动；每Tick到期项按到期时间及排程顺序先入FIFO，再入队正常就绪能力。额外发动不递归生成多重、不重复付魔法、不重置冷却，每次执行后独立触发回响；摧毁和转变取消即时及延迟队列。光环按当前 Runtime 求值，来源离开战场或被摧毁即失效；技能来源不依赖棋盘。回响执行产生的事件不再次触发回响。每场首次己方卡牌发动触发由能力状态保证至多一次。

来源卡牌持续状态获得回响由统一状态施加入口在正贡献实际累加后排队，按目标自身能力的TriggerStatus筛选；各次重复施加分别计数，排除备战、摧毁与回响施加，沿用FIFO及回响截断。能力复制与任务解锁保留状态筛选。

Runtime 只修改卡牌实际支持的属性，修改效果不能凭写属性赋予新能力。每次真实变化点记录冻结状态；同 Tick 保持原记录顺序，EventCount 对应原日志前缀。BattleResult 包含永久变化与只读播放状态，GameCoordinator.ResolveBattleForPlayback 通过 MatchResultService.Apply 一次结算，不因播放方式重跑。UI 回放细节见 UI 规范。

自身发动任务和永久成长只操作本场 Runtime。输入冻结已有任务进度，发动结束后推进并解锁，输出分别保存任务进度事件与精确永久属性贡献；应用结算写回真实己方实例并重算任务属性。任务身份奖励替换实例属性集中的只读身份对象，不更改共享定义；战斗快照独立保存身份变化和当前能力，回放不读取战后解锁结果推算旧画面。召唤和临时转变来源不会回写任务或成长。

飞行／狂暴独立存为 Runtime 的布尔字段，不进入数值属性字典。通用来源卡牌状态设置效果发布 CardStateChangedEvent，CardBattleSnapshot 冻结两个布尔值，回放投影传给 CardSnapshot。飞行减时长在目标状态施加处统一处理；狂暴只在发动的伤害公式结果与中毒／灼伤施加量处增加20%，不修改基础属性或周期结算。

战斗快照按非持续能力的效果顺序冻结当前数值施加量，包含公式、有效光环和狂暴，回放投影到CardSnapshot，UI只格式化数值。MatchDisplayQuery通过BattleSetupFactory与CombatSimulator.Preview复用同一只读光环求值，不发动能力、不推进随机、不执行开战效果、不回写实例；无已知对手时保留依赖对手的比例说明。备战区展示自身实例数值，不投影战场加成。

## 验证与审查

### 服务端程序与持久数据

服务端程序与玩家数据必须分开存放。建议程序发布在 `/opt/project-star/releases/<version>/`，当前版本通过独立入口指向；玩家文件存档、业务持久文件与身份保护密钥放在 `/var/lib/project-star/`，PostgreSQL 数据使用独立的 `/var/lib/postgresql/` 或明确指定的持久数据卷。服务程序只读发布目录；运行时写入位置必须显式配置，不依赖当前工作目录。

配置与凭据独立放入 `/etc/project-star/`，日志与缓存分别放入日志／缓存目录。源码、构建产物、output/、程序发布包和旧版本清理范围均不得包含玩家数据。发布与回滚只切换程序，数据库变更通过显式迁移执行；备份与恢复独立验证，不通过恢复旧程序代替恢复数据库。具体目录、权限与验收清单见[服务器计划](../planning/ASYNC_PVP_SERVER_PLAN.md)。

### 规则与集成验证

Main.tscn 保留分类/全部验证及 headless --verify。Presentation/Verification 中按领域职责划分规则检查和共享夹具，PhaseOneVerification 只组装列表和显示结果；场景集成仍在 Godot 中验证。

重点覆盖不可变定义与实例隔离、内容身份与快照传递、Modifier 精确堆叠/移除、经济失败原子性、棋盘推挤与回滚、遭遇随机/历史、FIFO/回响、状态周期、日蚀/寂灭、永久变化与确定性回放。架构迁移保留既有验证，并比较固定输入的结果、事件顺序和冻结状态。

审查时检查状态所有者、Match/Battle 生命周期、事务失败路径、随机/时间/遍历顺序、跨层写入、能力复用、快照隔离和实际扩展收益。BUG 登记及提交约定见 AGENTS 与开发约定。
