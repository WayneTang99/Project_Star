# 权威对局存档与操作契约

更新：2026-10-23。已实现创建、商店与构筑、选项事件、怪物战及真实快照PvP的权威保存恢复；两个正常构筑账号各完成两轮PvP并消费对方快照。Main联网面板已接首条客户端链并进入第二轮，使用与验证见[ONLINE_CLIENT](ONLINE_CLIENT.md)；完整棋盘／回放及全部可达操作仍待办。用户当前没有域名，API仅监听云端回环，开发客户端经SSH隧道访问，公网HTTPS延后。PvP任务与匹配契约见[PVP_SERVER](PVP_SERVER.md)。

## 1. 当前实现

`ServerMatchService`是应用用例，复用正式创建、收入、回合经验、遭遇排程、商品池、购买／自动放置／合并、棋盘推挤及出售逻辑。额外启动资金为0，默认英雄第一轮收入5、第一回合经验1，不使用本地试玩999金币。

`ServerMatchSession`在应用层拥有当前访问阶段、商品、选项事件、怪物战及PvP状态；旧本地Presenter的页面状态不直接作为服务端状态。开放已下发的卡牌商店、购买、移动、出售、刷新、离店、选项事件、怪物战和PvP，不接受客户端提交价格、seed、实例数值、胜负或完整存档。导师／宝石访问、主动结束对局入口继续待办；PvP自动终局已归档并允许重开。

选择遭遇时，既有`EncounterScheduler.Select`已经推进Round/Turn；`leave_shop`只调用Generate结算当前边界并生成候选，不再递增回合。成功后清除Shop和ShopKey，回到EncounterChoice；无需先购买或留有余额。Generate失败时丢弃本次候选模型，API不提交其状态；同一requestId重试返回原回执，新requestId在非商店阶段离店返回422，不能反复抽取候选。

选项事件按`EncounterChoice → Event → EncounterChoice`推进。`select_event`只接受已下发且定义为ChoiceEncounterDefinition的候选，生成稳定visitId并保存实际展示槽、等级与发生轮回合；恢复不重抽展示槽。`resolve_event`只接受本次访问的已展示optionKey，复用正式结算并保存完成标记与结果摘要，不保存可变CardInstance。未结算不能离开；完成后换新requestId也不能再次领奖。`leave_event`只生成已经推进边界的候选，不再增加回合；事件阶段允许整理／出售。

探险加权结果若引出怪物，直接转入MonsterPreparation，保留原事件轮回合并写入battle.sourceEncounterKey；不再次调用遭遇Select。没有同等级同轮次范围怪物时，沿用现有规则完成事件并返回monsterEncounterSkipped。选项发卡满盘时沿用现有规则返回cardRewardSkipped，**不会自动转成怪物待领奖励**；这与怪物胜利掉落保留待领是两种已有行为，本轮没有调整玩法。

怪物战阶段为`EncounterChoice或Event → MonsterPreparation → MonsterResult → EncounterChoice`。选怪时保存战斗UUID、发生轮／回合、种子及怪物完整实例（包含稳定ID、卡组、技能与布局），恢复不调用对手工厂重抽。准备阶段仍允许移动／出售己方卡牌，固定的是对手与战斗种子；开战读取最后已提交的己方模型，不沿用过期棋盘。第4回合直接选怪后进度已到5，battle.turn仍为4；事件引出的怪物则记录事件实际回合，进度同样只领先一次。结算不再次推进回合。客户端提交战斗ID，应用层只允许准备阶段模拟并复用正式`MatchResultService`回写成长、按损血比例发资源及胜利掉落。结果阶段重启或换新requestId也不能再结算。

结果摘要与奖励随对局、版本、请求回执同事务提交。`claim_monster_reward`复用原有获得／合并用例；满盘失败保留待领项及版本。战后可移动或出售腾位，再以新requestId和最新版本重新领取；原失败requestId仍返回原失败回执。`finish_monster`要求处理完待领奖励，只生成原遭遇下一回合候选并结算一次回合经验，不自动丢弃奖励。怪物结果仍仅有摘要；PvP任务已另行保留双方冻结输入，但两者均无客户端完整回放下载协议。

PvP按`EncounterChoice → PvpPending → PvpResult`推进；参战固定双方输入、战斗ID、种子和对手快照，待结算时禁止编辑。后台在事务外模拟，提交时校验令牌及原版本，原子写回成长／胜场／声望／终局、结果和开战前快照。`finish_pvp`才发下一轮收入／经验并生成候选，不能在选择或结算时提前补发。自动终局保留按ID可读的旧局，释放进行中唯一索引；终局待领奖励保留但不再开放领取。技术失败不判玩家输，retry保持原输入，详见[PvP事务](PVP_SERVER.md#3-持久任务与事务)。

`move_card`复用`BoardService.PlaceCard`的跨区与完整推挤事务，失败保留源位置。`sell_card`复用`SellFromBoard`，从服务器当前实例计算最终价值并一次结算出售能力、任务和成长；客户端不决定返还额，预期版本拦截旧确认。选遭遇、商店、事件、怪物准备、怪物结果及非终局PvP结果阶段均允许这两项，待结算和已结束的对局拒绝。

`refresh_shop`只在当前卡牌商店有效，复用一次付费刷新规则。服务器重新排除整批旧商品key（含已售罄）和当前满级库存，候选不足三个时返回实际数量；费用、原候选池、刷新次数与随机状态保存在原存档字段。响应`shop.refreshCost`为费用，`shop.canRefresh`同时反映候选、次数和当前余额；查询不消耗随机或扣款。

Godot 常驻无图形规则进程在主线程串行执行应用用例，stdin/stdout 为本机私有白名单协议。每次从数据库存档重建独立模型，返回候选新状态；API 在数据库提交成功之后才返回结果。Godot 进程不写数据库，也不保存唯一的权威状态。

规则包不含图形资源；宿主进程与规则包共同处于只读发布树。数据库仍独立位于 `/var/lib/postgresql/18/main/`。没有使用本机 WSL。

## 2. 存档字段审计

存档格式4由 `MatchSaveCodec` 显式维护，展示用 `MatchSnapshot` 不可直接代替。以下状态按当前源码逐项审计：

| 源位置／状态 | 保存或恢复方式 |
|---|---|
| MatchSession.Id | 保存稳定UUID，恢复不另建新身份 |
| Definitions、GemDefinitions | 按固定规则程序集中的正式Definition重建，不复制第二套内容数据 |
| Random.Seed、State | 无精度损失的十进制字符串；恢复不抽随机数 |
| Progress.Round、Turn、PvpWins | 显式字段 |
| IncomeSettledThroughRound、ExperienceSettledThroughTurn | 显式结算游标，避免恢复重新发收入／经验 |
| Player.Resources | 基础值与每条Modifier分开保存，含财富、经验、声望及其他已写入属性 |
| Hero.Id、Identity、Persistent、BaseCombat | 身份ID和定义key、独立属性集；展示身份来自固定Definition |
| Card.Id、Definition、Level | 原实例ID、正式key与等级；按等级恢复能力及销售／获取／胜利配置 |
| Card.Persistent、BaseCombat | 保存基础值及Modifier的ID、来源、属性、量、生命周期；保持原贡献累加顺序 |
| Card.Quests、身份变化、Tags | 保存每项进度；同版本任务定义恢复已解锁元素／标签，不重新触发游戏事件 |
| Card.CooldownMultiplier | 原始decimal，恢复不再次执行出售缩减 |
| Card.GemSockets | 按孔保存宝石key或null，恢复相同Definition及原孔位 |
| Skill.Id、Key、Level、Persistent、BaseCombat | 保留ID和属性贡献，能力从同版本Definition重建 |
| Board.Battlefield、Bench | 容量、实例归属、起始位置、占格和可推挤状态；拒绝重叠、越界或丢失实例引用 |
| EncounterSchedule.SeenKeys、CurrentChoices | 原历史集合与已下发候选完整保存，不重抽 |
| PendingMonsterRewards、PendingBattleMaxHealthBonus | 按原值保存；不自动领取或消费 |
| AppliedCardSetModifiers、AppliedCardQuestModifiers | 保存关联贡献ID，后续仍能精确撤销 |
| GemShopRevision | 保留修订游标，不因恢复回到0 |
| Status、Summary | 显式保存；PvP自动胜负终局已接入，完成轮数按发生轮记录 |
| ServerMatchSession.Phase、ShopKey、Shop | 保存阶段、商店等级、原候选池、商品key／等级／价格／售罄、刷新费用与次数 |
| ServerMatchSession.Battle | 保存战斗UUID、怪物key、发生轮回合、可空来源事件key、种子、完整对手存档和可空结果摘要；怪物英雄按MonsterDefinition重建，不误当可选英雄 |
| ServerMatchSession.Event、EncounterOptionSet | 稳定访问UUID、事件key、发生轮回合、访问等级、有序实际展示optionKey、完成标记和小型结果；按同版本Definition重建选项效果，拒绝不属于原展示槽的key |
| ServerMatchSession.Pvp | 战斗UUID、发生轮、seed、完整固定对手、来源快照ID／测试标记和结果摘要；恢复校验轮次、阶段及来源一致性，禁止对手内嵌战斗或访问 |
| MatchSession.Events | 当前仅为进程内诊断事件；无正式业务读取依赖，不把它当恢复或结算事实来源，不持久化历史列表 |
| ActiveMentorVisit、ActiveGemShopVisit | **尚未实现其持久化协议；出现非空状态时保存直接失败**，不能静默丢弃 |
| 播放句柄 | 旧本地播放状态不作为服务端状态；完整回放继续待办 |
| AppliedMatchRewards 的 BattleResult 引用集合 | 不保存进程内引用缓存；联网入口以战斗ID、持久阶段、预期版本及回执防重复，不能靠此缓存跨进程去重 |

完整P2仍需导师／宝石访问与全部可达操作的恢复验证。格式4未实现旧格式迁移；与既有规则版本隔离一致，旧局保留并返回409，不悄悄重新开局。PvP完整冻结输入与开战前快照由独立任务表保留，不以展示视图替代。

## 3. 数据库事务与幂等

结构版本2新增 `matches` 与 `match_requests`，结构3再增加PvP快照与任务；迁移保留原账号、会话和对局数据。部署前分别生成`pre-p2-20261023.dump`及`pre-p4-20261023-01.dump`，均在云端独立备份目录。仅支持结构2的旧程序不能作为当前结构3的回滚目标。

- `matches_one_active_player` 是 `status='active'` 的玩家唯一索引，数据库保证每个内部玩家最多一局进行中对局。多个登录会话不改变此限制。
- 每个状态修改在事务中先锁定该玩家的操作序列，读取原请求回执，再检查归属、规则版本和预期版本。
- `requestId` 在同一玩家的创建／对局命令间共用唯一空间；对识别后的规范字段计算SHA-256摘要。同ID同内容返回原HTTP结果，同ID不同内容返回409。
- 新增可选BattleId／RewardIndex／CardId／TargetZone／TargetStart／VisitId／OptionKey只在有值时参与摘要，旧命令保持原有规范摘要；避免程序升级后误判原requestId被改写。历史回执查询仍先于规则版本检查。
- 候选存档、递增版本和请求回执同一事务提交。可判定的业务拒绝也记录回执；内部故障／超时返回503并回滚，客户端可用原requestId重试。
- 读取从认证会话取得玩家ID，不接受自定义身份头；命令的对局ID必须属于该玩家。读取不发奖励、不推进随机数、不修改数据库。
- 账号回执当前没有清理期限；后续制定存档及幂等保留策略时，不能提前删除仍可能重试的回执。

## 4. HTTP 契约

以下接口均使用 `Authorization: Bearer <accessToken>`；认证与错误外壳见[账号服务](ACCOUNT_SERVER.md)。

| 接口 | 输入 | 成功结果 |
|---|---|---|
| `POST /v1/matches` | `requestId` UUID、`heroKey` | 201，版本1及当前状态；重复同请求仍返回原201 |
| `GET /v1/matches/current` | 无 | 200，当前已提交状态；无进行中对局404 |
| `GET /v1/matches/{id}` | 对局ID | 200，本人进行中或已结束对局 |
| `GET /v1/battles/{id}` | PvP战斗ID | 200，本人任务状态及可空结果 |
| `POST /v1/matches/{id}/commands` | `requestId`、`expectedMatchVersion`、`type`及对应参数 | 通常200并递增版本；PvP接受／重试202，retry不增加对局版本 |

当前命令如下：

| type | 参数 | 允许阶段 |
|---|---|---|
| `select_shop` | `encounterKey` | EncounterChoice，已下发卡牌商店 |
| `buy` | `offerIndex`，0～2 | Shop |
| `move_card` | `cardId` UUID、`targetZone`（严格为`Battlefield`／`Bench`）、`targetStart`（0～9） | EncounterChoice／Shop／Event／MonsterPreparation／MonsterResult／PvpResult；对局须进行中 |
| `sell_card` | `cardId` UUID | 同上 |
| `refresh_shop` | 无 | Shop，尚未刷新且有足够余额／新候选 |
| `leave_shop` | 无 | Shop |
| `select_event` | `encounterKey` | EncounterChoice，已下发选项事件 |
| `resolve_event` | `visitId` UUID、`optionKey` | Event，当前访问未完成且选项已展示 |
| `leave_event` | `visitId` UUID | Event，已结算 |
| `select_monster` | `encounterKey` | EncounterChoice，第4回合已下发怪物 |
| `resolve_monster` | `battleId` UUID | MonsterPreparation |
| `claim_monster_reward` | `battleId`、`rewardIndex`，0～255 | MonsterResult |
| `finish_monster` | `battleId` | MonsterResult，待领列表为空 |
| `start_pvp` | `encounterKey` | EncounterChoice，第8回合已下发PvP，202 |
| `retry_pvp` | `battleId` | PvpPending，对应任务failed，202；固定原输入且版本不变 |
| `claim_reward` | `battleId`、`rewardIndex` | 非终局PvpResult或MonsterResult |
| `finish_pvp` | `battleId` | 非终局PvpResult，待领列表为空 |

状态响应为`matchId`、`version`、`rulesVersion`、`state`；state含英雄、阶段、轮次、资源、pendingBattleMaxHealthBonus、卡牌、技能、待领奖励、候选、商品、eventVisit（访问ID／发生边界／等级／已展示选项／完成标记／结果摘要）、battle、pvp及matchSummary，不包含内部随机状态／完整存档。pvp含发生轮、对手展示、来源标记及结果；规则选择已推进时应按pvp.round展示本场发生轮。候选新增isChoiceEvent用于识别可选项事件。客户端不得提交伤害或胜负；rewardIndex来自当前版本的待领奖励列表，optionKey来自当前eventVisit.options。

移动示例：`{"requestId":"<新的UUID>","expectedMatchVersion":3,"type":"move_card","cardId":"<己方卡牌UUID>","targetZone":"Bench","targetStart":0}`。坐标通过HTTP边界后，卡牌占格、目标空间及推挤可行性仍由应用用例校验。卡牌已出售、属于其他对局或无可行推挤计划时422且不改状态；同玩家两个不同意图争用同一版本只有一个成功。

离店示例：`{"requestId":"<新的UUID>","expectedMatchVersion":3,"type":"leave_shop"}`。成功增加一次版本，shop为null，返回已经固定的新候选。旧商品命令在旧版本下409，在新版本但已离店时422。历史请求重试仍返回当时的原回执，客户端不能用低版本历史响应覆盖已经取得的高版本状态；必要时重新查询current。

未知命令或缺失参数400；未认证401；跨账号／不存在对局404；已有进行中对局、版本冲突、不兼容规则、复用请求ID改内容409；合法请求但规则拒绝422；内部／工作进程失败503。503后保持同requestId、同参数重试，不能通过修改请求ID重抽。

## 5. 规则版本与故障恢复

规则版本为导出 `Project_Star.dll` 的SHA-256；规则进程、内部请求和存档必须匹配。API启动还校验程序集实际摘要，不能仅改版本文本冒充旧规则。

当前策略是**拒绝加载其他规则版本的旧局，保留数据，返回409**。它是防止误读的开发阶段边界，不是已确认的正式旧局运营政策。不同规则更新后，旧qa测试局会被隔离；正式发布前需决定继续运行旧版本、迁移或结束旧局，并实现路由／迁移。当前存档恢复证据包含重启及同一规则构建的程序切换／回滚／清理，不代表跨规则升级已经支持。

单个规则调用含排队最多20秒；工作进程异常会被丢弃，后续请求或健康检查重建进程。强杀瞬间首个请求可能503，下一请求恢复；测试核对数据库摘要不变。当前工作进程并发为1，外层请求并发最多8，未做500人容量认证。

当前怪物模拟仍在持有玩家事务锁期间同步运行，失效候选整体回滚。PvP已采用独立冻结输入、持久租约任务与事务外模拟，结果原子提交；异常重试、重启接管及故障回滚证据见[P4记录](../quality/ASYNC_PVP_P4_2026_10_23.md)。当前并发与资源未通过容量验收。

## 6. 构建与当前部署

云端先构建.NET10宿主到 `account-publish/`；再用官方Godot4.7.2.NET编辑器、官方Linux模板和原.NET8SDK构建规则包：

```bash
export DOTNET_ROOT=/home/ubuntu/project-star-p0/dotnet
export PATH="$DOTNET_ROOT:$PATH"
bash <源码>/scripts/Infrastructure/Server/BuildRulesWorker.sh \
  <源码> <官方Godot.NET编辑器> <官方linux_release.x86_64> <新的源码外输出目录>
cp -a <输出目录>/rules /home/ubuntu/project-star-tools/account-publish/
bash /home/ubuntu/project-star-tools/InstallAccountHost.sh \
  /home/ubuntu/project-star-tools/account-publish p4-YYYYMMDD-pvp-NN
```

复制前确保目标 `account-publish/rules/` 不存在或使用全新的发布暂存目录，避免重复嵌套复制。安装脚本在改数据库前运行最终包的存档验证，再显式迁移、切换程序及检查就绪；配置和数据库均在发布树之外。脚本不自动把兼容性未知的数据库降级。

当前程序为`p4-20261023-pvp-01`，结构3、格式4，最新隔离源码为`/home/ubuntu/project-star-tools/pvp-source/`；最终包暂存在`pvp-publish/`并已同步`account-publish/`。证据和规则摘要以[P4记录](../quality/ASYNC_PVP_P4_2026_10_23.md)为准。新增规则包和.NET宿主尚未加入GitHub Release自动附件，不写入代码分支。
