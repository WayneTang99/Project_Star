# 内容数据规范

正式内容统一维护在 [scripts/Content/](../../scripts/Content/) 的 C# Definition 中，是运行时与编辑时的唯一数据来源。玩法以 [游戏规则](GAME_DESIGN.md) 为准，命名以 [术语表](GLOSSARY.md) 为准。

描述是展示文本，不从中文描述解析能力；行为组合通用 Ability / Effect。修改内容时更新对应 Definition 和有意义的行为验证，字段或维护规则变化时更新本文。Godot 验证入口检查注册、只读隔离、实例与快照传递、资源存在性和实际效果。

随机敌方持续状态效果可配置目标数量，一次效果不重复选择，候选不足时影响全部可选卡牌；备战区和已摧毁卡牌不参与抽选。双方战场元素冷却光环在战斗层动态计数，以贡献差值调整来源冷却，不回写卡牌实例。

`ApplyStatusToAdjacentAlliedCardsEffectDefinition.RequiredTag` 可选地限制目标标签；为空时维持左右直接相邻己方存活战场卡牌的原有范围。标签筛选与 `BonusTag` 时长倍率独立，`BonusMultiplier = 1` 表示不翻倍；每次发动重新筛选，状态时长仍累加并自然到期。

`ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition` 配置标签与每张卡的缩减Tick数，仅允许战场自身光环。按己方存活战场卡牌有效标签动态计数，包含符合条件的来源自身，排除敌方、备战与已摧毁卡牌；复用冷却贡献差值与永久倍率规则，不回写对局实例。

内容增删改查使用项目内的 [project-star-content-definition Skill](../../.codex/skills/project-star-content-definition/SKILL.md)：查询从当前定义读取数据，修改和删除追踪实际引用，内容变更执行构建与行为验证。查询结果可以临时展示或按需导出，内容数据仍统一维护在 Definition。

## 卡牌

`IncreaseHeroMaxHealthPerAlliedTaggedCardEffectDefinition`按己方存活战场卡牌的有效标签数量提供英雄最大生命光环，包含符合标签的来源自身；备战、敌方和已摧毁卡牌排除。多个来源累加，数量或来源有效性变化时按总贡献差值更新最大生命和当前生命，保留已损失生命量，撤销加成时最低保留1生命，不复活已死亡英雄；仅本场有效，不回写对局。备战英雄展示复用领域层只读光环预览。

`MultiplyReceivedCardStatusDurationEffectDefinition` 配置持续状态与正整数倍率，仅允许战场自身光环，可复用于疾速、迟缓、禁锢。目标每次实际获得状态时，将施加来源的时长加成及该次施加倍率计算后的新增贡献再乘目标光环倍率；多个目标倍率相乘，飞行减时长在其后处理。只影响新增贡献，已有时长继续自然衰减，不追溯放大或撤销，不写回对局；来源备战、摧毁或转变失去光环后不再提供倍率。

全部卡牌文案遵循[术语表的卡牌文案规范](GLOSSARY.md#卡牌文案规范)。目标定语前置；固定数值效果的详情可以显示数值，特殊机制详情保留当前等级的公式并显示当前适用加成，卡面合并显示冻结快照的最终数值。特殊机制不能因采用公式而遗漏适用的属性、光环或宝石加成；同一效果的机制值与加成不得拆成重复的卡面数值。动态公式的来源、比例、消耗、取整、运算顺序与持续性不能被详情中的当前结果代替；不从中文文案反向解析能力。

`ModifyAlliedAttackCardsAttributeEffectDefinition` 对全部己方存活战场攻击卡牌累加指定本场属性，不消耗随机数；沿用战斗层按伤害能力判定的攻击分类，零攻击值仍符合，纯治疗／护甲辅助、备战、敌方及已摧毁卡牌排除。贡献仅本场保留，不回写实例或作为永久属性奖励；来源失效不撤销已结算加成。

`AttributeDamageEffectDefinition.Percent` 默认100，可配置1～100。按来源当次有效卡牌属性（包含其光环贡献）乘百分比后向下取整，再应用首次发动倍率及发动狂暴；读取卡牌护甲与读取英雄当前护甲是不同的效果。卡面与详情共用百分比计算，默认100保留既有属性攻击行为，不写回来源属性。

`SourceHeroArmorDamageEffectDefinition.Multiplier` 默认为1，按己方英雄当前护甲与可选属性加成之和乘正整数倍率造成伤害。`ConsumeArmor` 默认为false；开启时只用于已执行的敌方英雄伤害效果，先读取本次当前护甲、计算伤害，再扣除实际读取量，随后按普通伤害/无视护甲及狂暴配置结算。敌方隐身阻挡伤害不返还已失去的护甲；多重每次独立读取，不重复使用已消耗护甲。宝石主值在原发动效果执行前冻结，预览只计算数值不消耗护甲，战后不回写英雄战斗护甲。

`IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition.AdjacentOnly` 可选地按完整占格边界筛选来源左右直接相邻的己方存活战场标签卡牌，排除自身、敌方、备战及已摧毁卡牌；开启时仅允许战场自身光环。每张邻居各贡献一次，双方贡献累加，邻居失效后动态停止计入；不依赖其他卡牌的最终数值。关闭时保留全己方标签计数及包含自身的既有行为。战场卡面和预览复用战斗投影读取有效数值，不写回卡牌实例。

`EchoOnAlliedCardStatusGained` 以受影响卡牌的归属筛选己方存活战场来源，用`TriggerStatus`指定疾速、迟缓或禁锢。每张目标实际获得正时长时触发一次，包含来源自身、己方或敌方施加及多重；不包含备战、已摧毁目标、飞行取整后零时长或回响产生的状态。通用状态获得入口保存事件卡牌，来源无冷却且仅战场有效；与按施加方筛选的`EchoOnAlliedSlowApplied`分别维护，互不改变含义。

`IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition` 配置标签与每张直接相邻己方存活战场卡牌的冷却增加Tick数，仅允许战场自身光环。按完整占格边界分别计数左右邻居，复用冷却贡献差值与永久倍率规则，不回写对局实例。

`PassiveOnEclipseStart` 是本场一次性战斗触发；`EnterEclipseEffectDefinition` 可提前进入全场日蚀，重复请求不生效。进入增益复用来源属性倍率与 `IncreaseHeroMaxHealthPercentEffectDefinition`，后者向下取整增加目标英雄最大生命并等量增加当前生命。`MultiplyReceivedEclipseDamageEffectDefinition` 是英雄目标的日蚀伤害光环；按开战冻结的套装来源保持有效。`ReduceSetCardCooldownEffectDefinition` 由轮开始应用结算作用于该来源套装的战场成员，重复缩减按既有十进制冷却倍率累乘；相同卡牌key的多个实例不重复贡献套装计数，但各成员均获得缩减，备战不受影响。

`EchoOnSourceHeroFirstLethalDamage` 响应来源方英雄每场首次致命伤害，在护甲扣除、生命降至0后立即执行，早于死亡判定及同Tick后续伤害。直接攻击、灼伤、中毒与日蚀共用该入口；寂灭强制终止不触发。英雄仅有一次首次致命事件，按稳定顺序执行所有存活战场来源的匹配能力；备战及已摧毁来源不参与，多重不复制救援。`HealMaxHealthPercentEffectDefinition` 按目标最大生命百分比向下取整后累加来源治疗加成，治疗上限为最大生命；`MultiplySourceCardAttributeEffectDefinition` 按倍率累加来源卡牌当前本场属性的增量，保留已累计加成，仅作用于已支持的属性，不回写对局实例；持续光环仍按各来源独立求值。

`IncreaseHeroMaxHealthEffectDefinition`以正整数固定量增加英雄本场最大生命与等量当前生命，多次发动累加，不受治疗加成影响，不回写对局。默认作用于己方英雄目标；`TargetsSourceHero`可在敌方英雄目标的伤害能力中指向来源英雄，让伤害与生命成长共用一次发动、冷却和多重。持续光环及备战区不能执行此效果。

### 字段约定

`EchoOnEnemyCardActivated`可通过`TriggerCardAnyTags`限定敌方发动卡牌具有任意一个有效标签；未配置时仍监听所有敌方战场卡牌。标签按战斗Runtime求值，识别临时追加及光环标签；同一卡牌命中多个标签只触发一次，多重每次独立触发，回响产生的发动不再连锁。快照及等级预览保留该只读筛选列表。

`GrantTagToRandomEnemyCardsEffectDefinition`在执行时无重复随机选取敌方存活战场卡牌，保留原标签并追加指定标签，仅影响本场Runtime。候选不足时处理全部候选，备战和已摧毁卡牌不参与；已具有该标签的卡牌仍在候选中。标签随战斗快照冻结并用于回放及所有有效标签筛选，不回写对局身份。

`GrantSourceMulticastWhileEnemyTaggedEffectDefinition`为战场自身光环：敌方至少有一张存活战场卡牌具有任意指定有效标签时提供固定多重贡献，多标签或多目标不重复叠加；敌方不再满足条件或来源失效后停止贡献。与其他多重来源累加，额外发动次数仍在正常发动时冻结。

`IncreaseSourceAttributePerCooldownSecondEffectDefinition`是战场自身属性光环，读取来源最慢主动能力经固定增减、所有倍率和一秒下限计算后的有效完整冷却，以秒数乘`AmountPerSecond`后向下取整，作为独立贡献累加到已有属性。不读取当前倒计时，不写回基础属性，不允许修改冷却属性；来源离开战场或被摧毁后失效。人类数量、状态或冷却倍率变化后，战斗与只读预览均使用最新贡献。

| 字段 | 含义 |
|---|---|
| CardKey | 卡牌唯一 `StringName` key |
| 归属 | 英雄阵营 key；`neutral` 表示无阵营，不限制其他英雄使用 |
| 尺寸 | Small / Medium / Large，分别占 1 / 2 / 3 格，并自动生成尺寸标签 |
| 元素 | 1～2 个元素属性；`General` 表示无属性，不表示空值 |
| 额外标签 | 尺寸标签以外的卡牌标签 |
| 初始等级 | 默认创建、商店报价和普通获得时使用的等级 |
| 词条描述 | 定义保存为只读 `CardIdentityAttributes.DescriptionEntries` 列表 |
| 插画 | `CardIdentityAttributes.Illustration`：只读 `StringName` 资源标识，指向项目内原画的 `res://` 路径；由表现层加载纹理 |
| 宝石孔数 | `CardIdentityAttributes.GemSocketCount`：默认1，可显式指定非负孔数；非默认孔数需在卡牌自身说明中注明 |

宝石使用 `GemDefinition` / `GemIdentityAttributes`，key为 `gem.` 前缀；正式宝石置于 `scripts/Content/Gems/`，注册表扫描并校验重复key。只读身份保存名称和说明，定义另存追加发动效果与展示配色。卡牌实例独立保存有序孔位及只读宝石定义，快照逐孔复制名称、key和说明，不持有实例的可变数组；报价展示定义孔数及空孔。`GemShopDefinition` 配置遭遇等级、砧板数量上限及镶嵌费用，商品从已注册宝石中无重复抽选；种类不足上限时只展示已有种类。

斜杠分隔的数值从初始等级开始，依次对应到4级，不再重复标注等级。例如初始2级卡牌的 `8/7/6秒` 对应2/3/4级；初始3级卡牌的 `6/5秒` 对应3/4级。5级卡牌只记录5级数值。`—` 表示没有该项。

`AttributeDamageEffectDefinition.FirstActivationMultiplier` 可配置来源卡牌每场战斗首次成功主动发动的伤害倍率，默认1；倍率大于1仅用于主动发动。倍率作用于来源当前有效伤害属性，再按狂暴与护甲规则结算，不修改基础属性。开战、回响及魔法不足的失败尝试不消耗首次机会；多重额外发动计为后续发动。首次计数独立于其他卡牌，每场战斗重新生成；宝石主值及冻结施加量沿用同一伤害公式。

`EchoOnSourceHeroHealthBelow` 配置 `TriggerHeroHealthPercent`（1～99），在来源方英雄生命由不低于阈值降至严格低于阈值后，每个能力每场战斗立即触发一次；恰好等于阈值、致命伤害、备战及已摧毁来源不触发。先结算越过阈值的伤害，再按稳定来源顺序结算回响，早于同Tick后续攻击；治疗后再次跌破不会重复触发。`GainSourceHeroStealthEffectDefinition` 累加来源英雄隐身时长，回响不因多重重复；隐身伤害范围见游戏规则，剩余时长与受阻攻击事实冻结供回放使用。

描述每条保存“词条 key + 说明文字”，列表顺序就是展示顺序；一张卡允许多条相同词条。词条名称由 `CardKeywords` 统一维护，说明不重复写词条前缀，也不加入富文本标记。冷却、魔法消耗、条件、目标、分级数值和限制保留在说明中。

`ReduceSourceCooldownEffectDefinition`以正Tick数配置来源卡牌本场冷却缩减，可组合主动或回响；增量累加且不写回对局。完整冷却在固定量与所有倍率之后最低1秒，按完整时长差值调整剩余冷却，达到下限后不再额外扣减进度。`EchoOnSourceCardActivated`包含多重的每次延迟额外发动；多重在首次正常发动时冻结次数，每隔2 Tick释放一次并独立触发回响。

固定模板相邻召唤使用 `SummonAdjacentCardEffectDefinition`，可组合进战场自身的主动发动或战斗开始能力。执行时检查指定侧紧邻可用位置，使用来源等级创建临时卡牌；模板等级不支持、占格不足或超出边界时跳过，不覆盖存活卡牌，已摧毁卡牌的位置可复用。召唤物从自身完整冷却开始，仅本场战斗存在；周期发动的召唤描述归入发动词条。

`DestroyAlliedCardsAndDamageEffectDefinition` 按尺寸和元素筛选己方存活战场卡牌，仅在本场摧毁，并为每张实际摧毁的卡牌向敌方英雄结算一次伤害。每次发动先读取来源有效伤害属性一次，再摧毁本次全部目标；备战、敌方及已摧毁卡牌不参与，无目标时不造成伤害，多重额外发动重新筛选当前目标。

发动是自身CD结束触发；回响响应自身或其他卡牌发动所产生的事件，具体事件条件写在说明中；光环涵盖持续赋予及随状态计算，来源失效后的处理遵循该卡规则；拾取在获得此卡牌时结算；出售在出售此卡牌时结算；消耗表示使用后摧毁自身，默认仅本场战斗生效，效果按治疗等使用效果在前、摧毁在后的顺序组合。任务的实例进度与解锁机制保持独立。开战用于战斗开始触发，交易用于响应其他卡牌出售，历战用于战斗结束后的结算，凯旋用于参战获胜；被动仍为底层能力总称，正式描述按具体机制归类。词条组织描述，不生成或改变能力队列、回响连锁规则及对局结算。

正式定义通过有序 `CardDescriptionEntry` 列表保存词条 key、说明和顺序。只读条目随实例身份和 `CardSnapshot.DescriptionEntries` 传递，商店、棋盘、奖励和详情共用；UI不维护按卡牌key的文案表，也不解析中文标点来分段。描述保留各等级数值，当前等级与实例基础/当前属性另列，不在升级时改写身份文本。无描述条目的内部验证夹具可沿用能力说明。

### 按卡牌分类触发的充能回响

`EchoOnLeftmostAlliedCardActivated` 响应己方战场最左侧存活卡牌成功主动发动，包含多重。按发动开始时的战场位置判断，空格不影响排序，纯被动卡牌仍占首位；排除敌方、备战、技能及回响发动。`RightmostAlliedCard` 在效果执行时选择己方战场最右侧存活卡牌，包含来源自身，不要求相邻或具有主动能力；支持卡牌持续状态，复用时长累加与自然衰减。来源位于备战或被摧毁时不生效，首尾随存活战场卡牌变化重新选择。

`ChargeRandomOtherAlliedCardsEffectDefinition` 按配置数量无重复抽选己方其他存活战场卡牌并提供固定充能；可用可选 `RequiredTag` 限定目标标签，不配置时保持全卡筛选，空key无效。候选必须有尚在冷却的主动能力，排除来源自身、敌方、备战及已摧毁卡牌。候选不足时影响全部候选，沿用确定性随机与充能到零的即时发动队列。

`ChargeTaggedAlliedCardsEffectDefinition` 为匹配标签的己方存活战场卡牌逐张提供固定秒数充能，包含满足条件的来源自身，排除敌方、备战与已摧毁卡牌。充能只扣减当前主动能力的剩余冷却；由正值降至0时立即排队发动，已就绪能力不会重复排队。不改变基础冷却，产生的正常主动发动可触发既有回响。

`EchoOnMatchingAlliedCardActivated` 配置 `TriggerCardTag` / `TriggerCardElement`，匹配己方战场卡牌的主动发动；任一条件成立即触发一次，两项同时满足不重复触发。可配置 `TriggerCardSize`，尺寸条件必须同时满足；也可仅按尺寸筛选。包含满足条件的来源自身及多重额外发动，排除敌方、技能、备战区与回响产生的被动发动。筛选随能力冻结传入快照。`ChargeSourceCardEffectDefinition` 缩短来源主动能力剩余冷却，到零时立即排队发动；不改变基础冷却，不触发被动能力的冷却。

`EchoOnFirstAlliedCardActivated` 可用 `TriggerCardTag` 筛选首次成功主动发动的己方战场卡牌，每个能力每场仅触发一次；不匹配的卡牌不消耗机会，多重不重复触发。回响保留该次发动的事件卡牌。`ChargeTaggedAlliedCardsEffectDefinition` 支持卡牌及技能来源，对己方存活战场指定标签卡牌充能；配置 `ExcludeEventCard` 时排除本次发动者，仅用于首次发动回响，无其他目标仍消耗本场触发机会。

### 己方施加迟缓与双方攻击发动回响

`ApplyStatusToRandomAlliedCardEffectDefinition` 从己方存活战场卡牌中按战斗随机数选择一张，包含来源自身，排除备战与已摧毁卡牌；可选 `RequiredTag` 限定目标有效标签，未配置时保持原范围，空key无效。无目标时跳过且不消耗随机数。持续状态累加并自然衰减，迟缓与禁锢沿用目标飞行减半规则。

`SetRandomOtherAlliedCardStateEffectDefinition` 按本场冻结随机数等概率选择一张己方其他存活战场卡牌，设置飞行或狂暴布尔状态；排除来源自身、敌方、备战和已摧毁卡牌，不要求相邻。已有状态的卡牌仍可被选中，重复设置不重复发布进入事件；无目标跳过且不消耗随机数。与自毁效果组合时先设置目标状态，再摧毁来源；无目标也继续结算自毁。

轮初词条使用 `keyword.round_start`，能力使用 `PassiveOnRoundStart`，不进入战斗队列。轮收入边界先冻结当前战场卡牌的能力和等级，再各结算一次；备战区不触发，轮中获得或放入战场不补发。`GrantRandomOtherHeroSourceLevelCardEffectDefinition` 按指定尺寸筛选已注册英雄归属中当前玩家英雄以外、支持来源等级的卡牌，包含无阵营；按key稳定排序后使用对局随机数等概率选取。获得沿用同级合并及自动放置，无法放置时保留同级待领奖励；空池不消耗随机数。新获得或合并后的能力不参与生成它们的本次轮初。

`EchoOnAlliedSlowApplied` 响应己方成功施加的迟缓，按施加方筛选，与目标归属无关；每张目标实际获得正时长时触发一次，包含多重及重复施加。无目标、零时长、飞行取整后为0或敌方施加不触发；回响产生的迟缓不再触发回响。可组合 `ChargeSourceCardEffectDefinition`，充能降至0后按既有规则即时排队发动。

`EchoOnAnyAttackCardActivated` 响应双方战场攻击卡牌成功主动发动，包含多重；攻击分类沿用相邻攻击回响的直接伤害能力判定，同一卡牌的辅助主动发动也计数。`EventCard` 指向该次发动的卡牌，可施加迟缓／禁锢等持续状态；目标在回响执行前被摧毁或替换移出 Runtime 时跳过。纯辅助、备战、失败发动、技能、战斗开始与回响发动均不触发；来源在备战区或被摧毁后失效，无主动冷却或魔法消耗。

### 自身发动后的属性成长回响

`EchoOnSourceCardStatusGained` 用 `TriggerStatus` 指定疾速、迟缓或禁锢，响应来源卡牌自身实际获得正时长的每次施加，包括向已有状态累加、其他卡牌施加及多重；不要求由自身发动施加。无目标、零时长、备战或已摧毁目标不触发，回响产生的状态不再触发回响。来源须在战场区，无冷却与魔法消耗；触发筛选随能力与任务解锁快照冻结。

`EchoOnSourceCardActivated` 仅响应来源卡牌自己的主动发动，包含多重额外发动；不响应其他卡牌、技能或回响发动。`IncreaseSourceCardAttributeEffectDefinition` 累加来源已支持的战斗属性，发布属性变化事件，仅本场生效。当前整组多重次数在正常发动时确定，回响新增多重从下一组发动生效。

该成长效果可配置 `Permanent: true`：本场属性立即累加，贡献作为 `PermanentCardAttributeBonus` 返回，结算时叠加到己方仍在库存的真实实例，胜负均保留。召唤与临时转变来源的贡献仅本场生效，不写回原卡；不从最终攻击值反推永久加成。

`ApplySourceAttributePercentStatusEffectDefinition` 按当次来源有效属性（含现行加成）乘百分比并向下取整，向敌方英雄施加灼伤或中毒；复用状态累加及发动狂暴规则，不把属性百分比解析为展示文案。

### 相邻攻击卡牌发动回响

`EchoOnAdjacentAlliedAttackCardActivated` 响应左右直接相邻的己方战场攻击卡牌主动发动，包含多重额外发动，可用 `TriggerCardTag` 限定标签。攻击卡牌按其能力中是否包含直接伤害效果判断，该卡牌的每次主动发动均计数（包括同一卡牌的辅助主动能力）；伤害为0或全部被护甲吸收仍触发，治疗、护甲、状态施加等纯辅助卡牌不触发。相邻按完整占格边界判断，空格不跨越；排除敌方、备战区、技能及回响发动，来源被摧毁后失效。此回响无主动冷却和魔法消耗。

### 单侧相邻卡牌发动回响

`EchoOnAdjacentAlliedCardActivated` 通过 `TriggerCardSide` 指定左侧或右侧，响应该侧直接相邻的己方战场卡牌主动发动，包含多重额外发动，不要求攻击能力；可用 `TriggerCardTag` 限定标签、`TriggerCardSize` 限定尺寸；同时配置时必须全部满足，未配置的条件不筛选。相邻按完整占格边界判断，不跨空格；排除敌方、备战区、技能及回响发动，来源被摧毁后失效，无主动冷却与魔法消耗。左右两侧分别配置可组合为双侧相邻触发。

`LeftAdjacentAlliedCard` / `RightAdjacentAlliedCard` 在执行时选择该侧直接相邻的己方存活战场卡牌，无目标时跳过效果。可组合持续状态施加或 `ChargeCardEffectDefinition`；状态时长累加并自然衰减，充能复用剩余主动冷却扣减与到零即时排队，不改变基础冷却。充能产生的正常主动发动仍可触发回响。

### 敌方有护甲时的来源属性倍率

`MultiplySourceAttributeWhileEnemyHasArmorEffectDefinition` 为自身光环：敌方英雄当前护甲大于0时，按配置倍率放大来源有效属性（包含已有加成），不写回基础属性。每次伤害结算及冻结快照重新求值；护甲耗尽立即失效，再次获得护甲后恢复，多重的每次额外发动独立判断。

### 按己方标签数量增加来源属性

`IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition` 通过光环按己方存活战场卡牌的有效标签数量增加来源属性，包含满足标签的来源自身；备战区和被摧毁卡牌不计数。不写入永久属性或反复累加临时值，数量变化时重新求值。护甲发动使用 `GainSourceHeroArmorFromAttributeEffectDefinition` 读取含光环贡献的有效属性，已获得的英雄护甲不因卡牌数量下降而扣回。

### 己方卡牌属性光环

`ReduceAlliedElementCardCooldownAuraEffectDefinition` 在战场自身光环中配置元素与1～100的百分比，向己方匹配元素的存活战场卡牌提供冷却缩减。同类百分比累加至100%，与永久冷却倍率相乘；动态增减按总时长差值调整剩余冷却，首发、重置、冷却增量与冻结回放均读取有效倍率，不回写实例。

`IncreaseAlliedCardAttributeAuraEffectDefinition` 由有效来源向己方存活战场卡牌提供指定属性加成，包含具备对应属性能力的来源自身；只增强已有属性能力，多张来源按贡献累加，备战区及被摧毁卡牌不参与。有效值从基础值加各来源的定义贡献求得，再参与来源的条件倍率；来源失效立即停止贡献，不回写实例，也不依赖其他卡牌最终值。新召唤或转变卡牌立即按当前有效来源求值。

该光环支持卡牌、技能及套装来源；技能使用 `AlliedHero` 目标。可选 `ExtremeSide` 按当前存活战场中具备对应属性能力的卡牌位置选择最左或最右一张；空目标无贡献，摧毁、修复、召唤及转变后重新求值。`PoisonBonus` 与 `BurnBonus` 分别向中毒和灼伤施加量追加贡献，覆盖固定量、属性量及百分比量，每次施加只追加一次，再计算狂暴。

该光环可配置 `HeroLevelMultiplier`：每个来源贡献为固定量加来源方英雄等级×系数。英雄等级使用本场战斗输入冻结值，升级在下一场输入生效；卡面展示等级公式，受益卡牌的回放数值携带已计算的加成。

`MultiplyAlliedElementCardAttributeAuraEffectDefinition` 倍增己方指定元素的存活战场卡牌已有属性，支持卡牌、技能及套装光环来源。可选敌方标签条件按存活战场卡牌的有效标签判断，任一标签满足即生效，不按敌人数重复叠加；备战区和被摧毁卡牌不计数。各有效来源倍率相乘，作用于包含加成的有效属性；标签、召唤、转变和摧毁变化后重新求值，不回写实例，回放读取冻结结果。

治疗使用 `HealingBonus` 记录对已有 `HealEffectDefinition` 的额外治疗量：每次治疗为效果基础量加有效治疗加成，受目标最大生命限制，不提高魔法恢复或再生。冻结快照携带该加成，回放卡面显示实际治疗数值；已治疗生命和已施加英雄护甲不会因光环失效而扣回。

`HealEffectDefinition.TargetsSourceHero` 可让治疗指向来源方英雄，组合在以敌方英雄为目标的伤害能力中；伤害和治疗共用一次发动、冷却及多重，治疗仍受己方最大生命限制并累加现有治疗加成。

### 布尔战斗状态

`ReduceSourceCooldownWhileInStateEffectDefinition` 在战场自身光环中配置 `Flying` 或 `Berserk` 与正整数缩减Tick。状态成立时缩短来源主动冷却，退出状态或来源失效时撤销该贡献；总冷却最低为0。状态变化按总时长差值调整剩余冷却、保留已走过的进度，再与永久倍率和元素冷却光环相乘；重置及冻结回放读取相同结果，不写回实例。

`ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition` 配置指定标签与正整数缩减Tick，仅用于战场自身光环。左右按完整占格边界检查直接相邻的己方存活战场卡牌；任意一侧满足即缩短一次，两侧满足不重复。条件随标签获得、召唤、转变与摧毁动态求值，条件失效撤销自身贡献；冷却与剩余进度调整沿用状态冷却光环规则，排除自身、敌方及备战区。

`IsFlying` / `IsBerserk` 是动态布尔值，默认否，不放入只读身份。能力组合使用通用 `SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying / Berserk, true / false)` 设置来源卡牌状态；可组合进发动、战斗开始触发或回响，不放入仅查询的光环能力，也不能由非卡牌来源设置。状态字段、变化事件与冻结快照都由战斗层维护，回放投影读取记录，不写模型。

具体减时长与数值加成规则见 [游戏规则](GAME_DESIGN.md)。状态仅由能力配置产生，不根据卡名或插画推断飞行／狂暴。进入状态回响使用 `EchoOnAlliedCardEnteredState` 与 `TriggerStateKey` 筛选真实的 false→true 变化，`EventCard` 指向该次事件中的卡牌；重复设置true不触发，回响产生的状态变化不再连锁。

### 战斗内转变

`TransformRandomEnemyCardEffectDefinition` 引用替换卡的不可变 Definition，随机选择同尺寸、替换定义支持其等级的存活敌方战场卡牌。新状态仅存在于 Runtime，保留目标等级和位置，清除旧能力、加成及状态并重新开始冷却。注册表校验替换内容已注册；冻结快照携带转变身份、标签和能力，回放直接投影，不修改对局实例。具体生命周期见 [游戏规则](GAME_DESIGN.md)。

### 己方标签卡牌属性强化

`ModifyTaggedAlliedCardsAttributeEffectDefinition.RandomSingleTarget` 可将全体标签强化改为随机一张；候选仅包括己方存活战场中有对应属性能力且匹配标签的卡牌，满足条件时包含来源自身。`RequiresAttributeSupport` 默认true，保留上述筛选；false时允许修改目标已经持有的战斗属性，即使没有使用该属性的能力，不新增原本不存在的属性。每次执行独立选择，增量累加到本场属性，无候选时跳过。

`ModifyRandomAlliedAttackCardAttributeEffectDefinition` 按伤害能力筛选己方存活战场攻击卡牌，零攻击仍可入选，满足条件时包含来源自身；不限制标签或元素。每次使用战斗随机数独立选择一张，累加指定属性，仅本场生效。无候选时跳过且不消耗随机数。可组合 `EchoOnMatchingAlliedCardActivated` 按元素监听己方成功主动发动，多重分别触发，回响不递归。

### 敌方卡牌发动的事件光环

`EchoOnEnemyCardActivated` 响应敌方战场卡牌成功主动发动，包含辅助卡牌及多重的每次额外发动；不响应己方、技能、备战、魔法不足等失败发动、开战能力或回响产生的被动发动。来源仅在存活战场区生效，无冷却或魔法消耗，沿用回响事件的FIFO及截断规则。

描述可以按卡牌需求使用光环词条，词条不改变底层事件触发。与本场属性强化组合时，每次触发向当前符合条件的目标累加增量；多来源分别贡献，来源失效后停止后续触发，已授予的增量本场保留，新出现的卡牌不补发过去事件的增量。

### 战斗开始的相邻召唤

`SummonAdjacentCardEffectDefinition` 在战斗开始的自身能力中配置不可变卡牌模板和左/右方向，召唤等级与来源相同。按来源完整占格边界检查该侧连续空位，不跨越、推挤或覆盖存活卡牌；备战区和敌方位置不阻挡，已摧毁卡牌的位置可复用。两侧效果独立、先左后右，多个来源按既有稳定顺序结算，先召唤的存活卡牌会阻挡后续召唤。注册表检查模板已注册并支持来源等级。

`SummonRandomAdjacentCardEffectDefinition` 提供多个模板，按等级、尺寸与该侧空位筛选后，使用战斗随机数等概率选一个召唤；无可放置模板时跳过且不消耗随机数。注册表逐个检查模板及等级，召唤物沿用相邻召唤的生命周期。

召唤物只创建战斗 Runtime，采用该等级基础数值、标签与能力，不继承来源加成；按完整冷却开始，立即参与光环及回响，不再次派发战斗开始能力。确定性ID由来源和召唤顺序派生，不消耗战斗随机数；冻结身份、位置、数值、能力与召唤事件供回放投影。召唤不是获得，不触发拾取、合并、任务或套装阈值重算，不产生永久卡牌变化，战后消失。

### 战斗内卡牌修复

`RepairRandomDestroyedAlliedCardEffectDefinition.RequiredTag` 配置非空标签，随机选择己方仅本场被摧毁、非备战且原位置全部占格空闲的匹配卡牌。目标沿用摧毁时冻结的标签；永久摧毁的真实卡牌不参与，召唤物仍按本场生命周期处理。无候选时不消耗随机数，每次执行只修复一张，组合伤害不因修复无目标而跳过。

修复保留既有Runtime的身份、数值、状态、能力和触发历史，恢复光环后按当前冷却倍率重置全部主动能力冷却；不重放开战能力，不恢复摧毁前取消的队列。`CardRepairedEvent` 冻结来源、目标、归属、原位置和占格，回放按召唤／修复事件的顺序恢复可见放置，战后不产生永久修复或库存变化。

### 参战胜利的永久加成

`PassiveOnMonsterVictory` / `PassiveOnBattleVictory` 是对局结算能力，使用开战时冻结的己方战场来源和等级，只在对应胜利后执行。`GrantSourceLevelCardEffectDefinition` 指定已注册的不可变卡牌模板并获得来源等级卡牌，复用获得、折半、拾取、自动放置与合并；满盘且无法合并时保留为待领取卡牌。备战、敌方、失败、平局及本场临时召唤不提供奖励；被本场临时摧毁的真实参战卡仍提供，已永久移出库存的来源不提供。新卡不计入生成它的本次战斗，同一结果的对局能力奖励只结算一次；战斗队列不执行这些能力。

等级定义的 `BattleVictoryBonuses` 使用通用 `BattleVictoryAttributeBonus` 配置属性和增量。应用结算根据战斗初始快照筛选己方战场区参战实例；怪物战与 PvP 获胜均累加永久 Modifier，失败、平局及备战区不触发，不要求卡牌实际发动。战斗内被摧毁仍算参战；被永久摧毁并移出库存的实例不再获得加成。升级保留已有贡献，并采用新等级的胜利增量。

等级定义的 `BattleValueBonus` 配置非负的参战后价值增量，零表示不提供此加成。应用结算按开战时位于己方战场区且结算时仍在库存的实例，将贡献累加到 `Persistent.Value`；怪物战和PvP的胜利、失败、平局均生效，不要求发动，战斗内被摧毁仍算参战。备战区和本场召唤物不参与，投降未生成战斗结果时不结算；升级保留既有贡献并采用新等级的增量。

以自身价值为来源的施毒复用 `ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Value)`。应用层将实例实际价值复制到只读战斗输入，战斗效果和回放读取该冻结值，战后价值增长仅影响下一场；卡面按当前实例价值显示施毒量。价值仍存于局外持久属性，不把战斗中的临时数值写回价值。

### 出售触发的永久加成

等级定义的 `SaleAttributeBonuses` 使用通用 `TaggedCardSaleAttributeBonus` 配置出售目标标签、来源属性与增量。来源须仍位于战场区或备战区；成功出售后累加来源实例的永久 Modifier，失败或重复出售不结算，来源自身被出售后不再触发。升级按新等级更新增量，并保留之前的永久贡献。交易预检检查加成溢出后再提交，不转入战斗状态或战斗回响。

`SoldValuePercent` 可将出售前最终价值的百分比（向下取整）加到固定增量；配置比例时空标签匹配任意卡牌。价值加成写入永久属性，其余加成仍写入基础战斗属性。`IncreaseAlliedCardAttributeAuraEffectDefinition.SourceValueMultiplier` 读取开战冻结的来源价值提供光环，`AttackCardsOnly` 可限定直接伤害卡牌；备战或摧毁来源失效，不读取其他卡牌最终值。备战预览随交易后的当前价值重新求值，旧战斗输入保持不变。

卡牌的 `Poison` / `Burn` 基础属性在此表示施加量，区别于英雄已经受到的中毒／灼伤。`ApplyAttributeStatusEffectDefinition` 从来源卡牌的冻结战斗属性读取施加量，卡面显示实例现值；已有战斗输入不会被后续出售改写。恢复魔法使用通用 `RestoreManaEffectDefinition`，累计至最大魔法上限后不再增加。

### 出售时战场攻击永久加成

`IncreaseLeftmostAttackCardOnSellDefinition` 在等级的 `OnSellReward` 中配置正整数增量。成功出售后，按己方战场占格起点从左向右选择第一张具有直接伤害能力的卡牌，为其 `BaseCombat.AttackDamage` 累加永久 Modifier。攻击分类与攻击发动回响一致，包含零伤害卡牌；跳过纯辅助卡牌与备战区，不选择已出售来源，无目标时仍成功出售。战场、备战及游离库存出售共用此规则，升级保留既有贡献，旧战斗输入保持冻结；预检合计同次出售的属性成长贡献，攻击溢出时整笔交易失败。

### 出售时英雄永久加成

`IncreaseHeroCombatAttributeOnSellDefinition` 在 `OnSellReward` 中配置属性key及正整数增量，成功出售后向己方英雄的局外 `BaseCombat` 累加永久Modifier。适用于战场、备战和游离库存出售，来源移除不撤销已授予贡献；升级刷新为该等级的出售效果。出售前校验英雄存在和属性溢出，失败不移除卡牌、不回补金币、不发放加成。新战斗采用累加后的英雄属性，已有战斗输入与回放保持冻结值。

### 出售时强化灼伤卡牌

`IncreaseLeftmostBurnCardOnSellDefinition` 配置正整数增量。来源移除后按己方战场起始格选择最左侧实际施加灼伤的卡牌，包含固定量、属性量、百分比量、回响及已解锁任务能力，跳过备战区与出售来源。向其 `BurnBonus` 累加永久Modifier，每次施加在基础量（百分比先向下取整）后加一次，再应用发动狂暴；不影响中毒或英雄现有灼伤。无目标正常出售，三种来源区域共用，目标升级保留；旧战斗和回放冻结，新卡面显示加成后的施加量。出售预检检查加成本身、同次交易成长和各灼伤效果的合计量溢出，失败不移除来源或发放金币。

### 出售时强化治疗卡牌

`IncreaseLeftmostHealingCardOnSellDefinition` 配置正整数治疗增量。出售来源移除后，按己方战场起始格从左到右选择第一张实际能力包含治疗效果的卡牌，向其 `HealingBonus` 累加永久Modifier；包含已解锁任务赋予的治疗能力，排除备战区与被出售来源，无目标时正常出售。各次贡献叠加，目标升级保留，新的战斗与卡面读取实际治疗加成，已有战斗输入保持冻结。出售前同时检查目标加成、此次其他出售贡献和治疗效果数值溢出，失败不移除来源、不发放金币或加成。

### 出售时强化疾速卡牌

`IncreaseLeftmostHasteCardOnSellDefinition` 按Tick配置正整数时长增量。成功出售并移除来源后，从己方战场最左侧查找第一张实际能力包含施加疾速效果的卡牌，向其 `HasteDurationBonus` 累加永久Modifier；包含主动、回响及已解锁任务能力，排除仅监听疾速获得的效果、备战区与被出售来源。无目标仍正常出售，三种来源区域共用此规则，目标升级保留贡献。战斗初始化读取冻结加成，每次施加疾速先累加基础时长，再应用效果自身的目标标签倍率；自身、相邻及随机目标均适用，不增加迟缓或禁锢时长。快照详情以秒展示增益，旧战斗与回放保持冻结。出售预检合计同次出售的属性成长贡献，并检查加成本身及最大单次时长（含标签倍率）溢出，失败不移除来源、不发放金币或加成。

### 出售时缩减目标冷却

等级定义可通过 `OnSellReward` 覆盖该等级的出售效果；未配置时沿用卡牌定义。`ReduceLeftmostElementCardCooldownOnSellDefinition` 指定元素与缩减百分比：成功出售并移除来源后，从己方战场最左侧向右查找第一张匹配元素的卡牌，仅对这一张生效。跳过非匹配元素，不查找备战区；无目标仍可成功出售。来源从战场、备战区或游离库存出售时共用此规则。

目标实例的十进制 `CooldownMultiplier` 默认1.00，每次乘以 `(100 - Percent) / 100`；不提前取整，升级保留，新实例重置。`ApplyStatusToRandomEnemyCardEffectDefinition` 随机向存活敌方战场卡牌累加疾速／迟缓／禁锢时长，沿用飞行减半及状态反应，无目标时跳过。

### 通用价值表

未显式覆写初始价值时遵循 [游戏价值规则](GAME_DESIGN.md)，此处不重复维护数值表。

### 内容维护约定

新增或修改正式卡牌时更新对应 `CardDefinition`，并检查：

1. CardKey 全局唯一，使用 `StringName`。
2. 归属 key 对应已有英雄阵营或 `neutral`。
3. 元素包含 1～2 个合法值。
4. 卡牌定义必须包含初始等级对应的数据。
5. 未特别说明时，“X级卡牌”表示初始等级为 X，并提供从 X 到4级的数据；“5级卡牌”表示固定5级且不能通过合并获得。
6. 能力、出售奖励和获得时加成使用通用定义，不在应用服务中硬编码卡牌专属判断。
7. 数值或描述变更后同步更新 Godot 内验证入口。
8. 正式卡牌的插画路径必须与内容定义一致且资源存在；尚未提供插画的内容使用共用 `res://art/ui/card-face/artwork/placeholder.svg`。快照传递该字段，UI 不按卡牌 key 硬编码图片。验证用内容可留空并显示占位图。

## 技能

### 字段约定

| 字段 | 含义 |
|---|---|
| SkillKey | 技能唯一 `StringName` key |
| 归属 | 英雄阵营 key；`neutral` 表示无阵营 |
| 初始等级 | 默认获得时创建的等级 |
| 支持等级 | 定义中可创建的等级；普通等级最高合并至4级，5级只能直接获得 |
| 描述 | 技能的触发条件和各等级效果，数值按支持等级从低到高排列 |
| 图标原画 | `SkillIdentityAttributes.Illustration`：只读 `StringName`，指向项目内独立方形原画的 `res://` 路径，由表现层加载纹理 |

技能独立于卡牌，不占战场区或备战区格位。首版技能只配置被动能力。`—` 表示没有该项。

每个正式技能必须配置独立的1:1图标原画，新增技能默认同时生成、检查并接入；仅用户明确要求时可暂缓。原画存入`art/ui/skills/artwork/`，保持细致明亮的手绘奇幻风格，主体在小图标下可辨认，不生成文字、边框或UI，不通过拉伸和裁切伪造方形比例。资源标识属于只读身份，随实例、`SkillSnapshot`、奖励预览和技能图鉴传递；不改变技能能力或等级数值。内部验证夹具可省略原画，表现层使用占位图。正式全池验证检查路径可加载、每个技能原画独立、实际宽高相等及全部支持等级的身份与数值传递。

战斗开始时对双方卡牌施加持续状态，可在一次 `PassiveOnBattleStart` 能力中依次组合 `ApplyStatusToRandomAlliedCardEffectDefinition` 与 `ApplyStatusToRandomEnemyCardEffectDefinition`（目标数量默认1）。各效果独立选择该侧存活战场卡牌，排除备战区与已摧毁卡牌，任一侧无候选不妨碍另一侧结算；持续状态复用累加、飞行减半及自然衰减，随机选择仅使用本场战斗的冻结种子，不影响对局随机状态。

`GainSourceHeroLevelScaledArmorEffectDefinition` 按战斗输入冻结的来源方英雄等级乘以配置系数，累加该英雄的战斗护甲。战斗开始能力每场触发一次，不依赖战场卡牌，也不回写英雄的局外属性；多个来源的护甲贡献相加。

### 内容维护约定

1. SkillKey 全局唯一，使用 `StringName`；归属 key 对应已有英雄阵营或 `neutral`。
2. 初始等级必须存在对应的等级配置；描述按支持等级顺序列出数值。
3. 技能效果复用通用能力，不配置主动能力或要求卡牌自身作为来源的效果。
4. 新增或修改正式技能时更新对应 `SkillDefinition` 和行为验证。

## 英雄

### 字段约定

| 字段 | 含义 |
|---|---|
| HeroKey | 英雄唯一 `StringName` key |
| 名称 / 称号 | 独立的只读身份展示文本，均使用 `string`；有称号时显示为“称号·名称”，无称号时只显示名称 |
| 插画 | `HeroIdentityAttributes.Illustration`：只读 `StringName` 项目资源路径，指向 `art/ui/heroes/` 下的英雄原画；传入快照供选角与英雄面板使用 |
| 归属 | 卡牌和技能内容筛选使用的阵营 key；不限制跨阵营内容的使用 |
| 初始等级 | 创建英雄实例时的等级 |
| 收入 | 每轮开始时结算的基础金钱收入 |
| 战斗初值 | 每场战斗开始时由英雄基础属性生成的状态；对局内永久加成可改变实际值 |

英雄不使用卡牌标签。金钱和声望属于对局资源，不是英雄定义的身份字段或战斗初值。

英雄未指定差异参数时，初始最大生命值使用200；升级及永久奖励在此基础上按各自规则累加。战斗开始时当前生命值等于该场实际最大生命值。

### 内容维护约定

1. HeroKey 和归属 key 使用 `StringName`；HeroKey 全局唯一，归属 key 与卡牌、技能的归属保持一致。
2. 只记录玩家可选的 `HeroDefinition`；怪物使用独立的 `MonsterDefinition`，不列为英雄。
3. 新增或修改正式英雄时更新对应 `HeroDefinition`，并验证内容归属、新局创建与战斗初值。
4. 新增英雄默认同时生成并接入独立原画，只有用户明确要求不生成时才跳过。使用内置图像生成工具，沿用现有英雄原画风格与方形构图；检查主体、实际尺寸和无文字／卡框后存入 `art/ui/heroes/`，在 Definition 中配置 `Illustration`，并验证纹理加载、快照传递、选角预览与英雄面板显示。执行说明见项目内容技能的英雄专项参考。

## 导师

导师正式内容位于 `scripts/Content/Mentors/`，使用独立 `MentorDefinition`；具体身份、默认等级与技能筛选规则以该目录下的定义为准。

| 字段 | 含义 |
|---|---|
| MentorKey | 导师唯一 `StringName` key，使用 `mentor.` 前缀 |
| 名称 / 原画 / 简述 | 保存于属性集的只读 `MentorIdentityAttributes`；原画为项目内 `StringName` 资源路径 |
| 等级 | 独立访问的默认导师等级，范围1～5；决定授予技能等级 |
| 筛选入口 | `CanOfferSkill` 实现纯判断，读取技能定义与英雄只读身份；不同导师可采用不同规则 |

通用用例负责支持等级过滤、最多三个不重复候选、确定性抽选及一次性领取，内容筛选不修改对局、不消耗随机数。导师无需配置遭遇就可注册和访问；定义与对应 `.cs.uid` 放入 `scripts/Content/Mentors/`，反射扫描自动登记，不添加按导师 key 分支的中央注册表。

## 遭遇

`ExplorationEncounterDefinition` 是 `ChoiceEncounterDefinition` 的探险内容子类，沿用 `Other` 排程、固定选项槽及加权结果结算，不增加独立的抽取池。新增探险通过正式 Definition 组合通用效果，已有探险的选项和概率不因分类调整而改变。

`ShopEncounterDefinition` 共用商店类型、等级和轮次配置；`CardShopEncounterDefinition` 扩展卡牌尺寸筛选，`GemShopDefinition` 扩展砧板候选及镶嵌费用。排程对两类商店统一读取共用基类等级，具体购买和镶嵌仍由各自用例结算。

`MonsterEncounterDefinition` 将独立 `MonsterDefinition` 投影为怪物遭遇，统一读取只读身份、等级和轮次范围；固定怪物回合与探险引出的怪物战共用该投影，不手工重复注册同名遭遇。怪物等级调整不改变卡组与技能条目配置的等级；本地对手英雄等级读取怪物等级。

`GainRandomWealthEncounterOptionEffectDefinition` 在包含两端的正整数区间内等概率抽取金币，使用对局随机状态；`GrantCardEncounterOptionEffectDefinition` 引用已注册且支持所配等级的卡牌 key，复用普通卡牌奖励的合并、放置与满盘跳过规则。同阵营随机奖励与指定卡牌奖励均可配置 `UseEncounterLevel`，开启时按本次访问等级筛选并授予，而非英雄等级或内容初始等级。指定卡牌展示名从正式卡牌目录读取，UI不按遭遇或卡牌 key 硬编码。

### 字段约定

| 字段 | 含义 |
|---|---|
| EncounterKey | 遭遇唯一 `StringName` key；怪物战使用对应怪物 key |
| 类型 | 商店 / 怪物战 / PvP / 可选项 |
| 原画 / 简述 | 原画资源 key 使用 `StringName`；用途简述使用 `string`，均位于只读遭遇身份，由 Definition 维护并随候选快照传递 |
| 轮次 | 可参与排程的轮次范围，端点包含在内 |
| 基础权重 | 在同类型候选池中的抽取权重；本局未出现过的遭遇按排程规则乘2 |
| 等级 | 商店自身等级、可选项遭遇等级或怪物等级；商店商品使用卡牌初始等级，且不高于商店等级 |
| 配置 | 商店商品池、战斗对手或可选项效果 |

导师遭遇使用 `MentorEncounterDefinition`，类型为可选项（`Other`），持有已注册的 `MentorKey`。包装定义位于 `scripts/Content/Encounters/`，独立配置遭遇身份、等级、轮次和权重；本次导师及授予技能均使用遭遇等级。注册表拒绝未知导师引用；导师不会自动投影为遭遇，只有显式包装才参与排程。

`—` 表示该类型没有对应等级。第4回合只抽怪物战，第8回合固定 PvP；其他回合从商店和可选项中生成候选，并保底至少一个商店。

### 定义组织

遭遇的排程、权重和配置保存在 `EncounterDefinition` 中；选项由 `ChoiceEncounterDefinition` 持有，保存展示槽、权重与通用效果。第4回合只抽怪物战，第8回合固定 PvP；其他回合从商店和可选项中生成候选，并保底至少一个商店。

`WeightedEncounterOptionEffectDefinition` 在玩家确认选项后，按 `WeightedEncounterEffectDefinition.Weight` 抽取一个结算效果；权重为正整数，总和不得超过int上限。它不参与展示槽抽选，选项始终可以固定展示。奖励效果 `GrantRandomTaggedCardEncounterOptionEffectDefinition.UseEncounterLevel` 可改用本次访问等级，默认关闭以保留既有固定等级奖励。

`EnterRandomMonsterEncounterOptionEffectDefinition` 从当前访问轮次范围内且等级等于本次访问等级的怪物中等概率抽取；无候选时返回提示并完成事件，有候选时进入该回合的怪物战准备，不再次推进轮回合。`EncounterOptionSet` 捕获访问等级与轮次，测试入口的遭遇等级覆盖同样生效。怪物 `MinimumRound` / `MaximumRound` 默认1～99，端点包含；第4回合候选及事件引出的怪物共用该范围。

怪物由 `MonsterDefinition` 自动投影为遭遇，不额外维护同名遭遇定义。试玩允许怪物不足三个；正式排程要求三个不同怪物。具体卡组、奖励与选项效果直接维护在对应内容定义中。

遭遇原画由 `EncounterIdentityAttributes.Illustration` 保存为只读 `StringName` 项目资源路径；怪物遭遇沿用 `MonsterIdentityAttributes.Illustration`，不重复登记。正式原画位于 `art/ui/encounters/artwork/`，排程将路径及商店等级传入 `EncounterChoice` 和对局快照，表现层加载纹理。未配置原画的内部夹具保留名称，正式内容验证资源加载。

商店、宝石商店、普通事件、导师包装及怪物的 Definition `Level` 为初始等级，默认解锁轮次依次为1／3／5／8／10（对应1～5级），并与 `MinimumRound` / `MaximumRound` 范围共同筛选。固定PvP保留每轮第8回合规则，不抽取等级。排程先按初始等级筛选，再用对局随机状态从初始等级至4级等概率抽取本次访问等级，与当前轮次开放的初始等级上限无关；每个候选只抽取一次，`Level` / `ShopLevel` 共用结果。测试等级覆盖不得降低初始等级，低于5级的遭遇最多提升到4级，初始5级的特殊遭遇保持5级；只有一个可用等级或采用覆盖时不消耗等级抽取随机数。

`EncounterChoice.Level` 将本次访问等级传入表现层；`ShopLevel` 仍仅表示商店的商品上限，与商店本次访问等级一致。进入遭遇后的页面快照保留遭遇等级，重开及生成下一组选项时清除旧遭遇信息。

### 内容维护约定

1. EncounterKey 在遭遇池唯一，选项 Key 在所属遭遇中唯一，均使用 `StringName`；怪物战的 EncounterKey 对应已注册的怪物 key。
2. 明确类型、轮次、基础权重和各展示槽；随机选项写明同一槽内的权重。
3. 商店等级与商品卡牌等级分开记录；怪物卡组和战斗属性以 `MonsterDefinition` 为准。
4. 新增或修改正式遭遇及选项时更新对应 Definition，并验证注册、排程和选项结算。

## 扩展与验证

`ReduceHeroStatusEffectDefinition` 对明确目标英雄扣减固定量灼伤或中毒，实际扣减为配置量与当前已有量的较小值，不产生负数，不吃施加量加成及狂暴，不触发状态获得回响。扣减以负增量记录状态事件，零扣减不产生事件；冻结卡面施加量读取当时可扣除的量，详情保留配置上限。可与治疗组合并随多重逐次重新结算。

`GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition` 为来源提供条件多重光环，以己方存活战场中是否存在匹配标签或元素的卡牌判断，包含来源自身，使用当前有效标签及元素。每份效果只贡献一次，多张匹配卡不重复叠加，不同效果独立累计；候选离场或被摧毁后动态失效，不回写基础多重。每次正常发动仍沿用统一规则冻结该组多重次数，条件变化不追溯取消已排入的额外发动。

`ModifyTaggedAlliedCardsAttributeEffectDefinition.AdjacentOnly` 可将标签属性强化限定为来源左右直接相邻的己方存活战场卡牌，按完整占格边界判断，排除自身、备战、已摧毁和隔空目标；每次发动及多重重新筛选。属性增量累加，仅本场保留，不读取其他卡牌最终值。

`EchoOnEnemyCardStatusGained` 按实际受影响卡牌的归属监听敌方存活战场卡牌获得指定疾速、迟缓或禁锢状态。复用正时长、飞行取整与回响连锁限制；每张卡牌每次实际获得分别触发，包含多重施加，零时长、备战、已摧毁及回响施加不触发。原有己方与自身状态获得回响保持原范围。

`TransformSourceIntoRandomCardsEffectDefinition` 在来源原占格内转变为若干张随机不同模板的同级卡牌。模板须尺寸一致、key不重复且支持来源等级，替换总占格须等于来源占格；候选不足或占格不匹配时不结算、不消耗随机数。按key稳定排序后无放回抽选，第一张沿用来源ID，其余使用确定性临时ID；取消旧排队与多重，新卡从完整冷却开始，清除原本战斗增益，不触发摧毁或亡语。只改变战斗Runtime，回放按转变身份更新占格后投影新增卡牌，对局库存及下一场恢复原卡。

`ApplyStatusToRandomEnemyCardEffectDefinition.RequiredTag`可按当前有效标签筛选敌方存活战场卡牌，随机选择不重复，数量不足时选全部，无候选不推进随机。`HealEffectDefinition.UnlessEnemyTag`仅在敌方没有该标签的存活战场卡牌时治疗，包含治疗加成，并在每次实际发动及多重时重新判断；备战与摧毁卡牌不阻止治疗。未配置这些字段时保留原行为，未知敌方时保留条件提示，已知敌方时冻结当前治疗施加量。

`TaggedCardAcquisitionAttributeBonus`在成功获得其他指定标签卡牌后，为战场区和备战区的来源累加永久属性；属于交易词条。购买、掉落、奖励与合并获得均每次结算一次，不补计历史库存，游离库存来源、合并消耗及失败获得不触发。各获得入口先校验全部成长贡献的基值和最终值溢出，再提交获得及成长；升级替换当前等级增量并保留已有成长。自身获得效果仍使用拾取词条。

`IncreaseAlliedCardAttributeAuraEffectDefinition.RequiredTag`可限定受益卡牌的当前有效标签，未配置时沿用原范围；仍只增强目标已有对应属性能力，包含零基值但支持该能力的目标。`SourceValueMultiplier`读取开战冻结的来源当前价值，多来源累计，来源在备战或被摧毁时不提供贡献，旧战斗及回放不受后续价值成长影响。

相邻发动回响使用 `TriggerCardTag` 筛选发动者；相邻效果目标可通过 `TargetCardTag` 独立限定标签。目标仍按完整占格边界选择直接相邻的己方存活战场卡牌，标签不匹配时跳过效果，不跨过该卡牌寻找其他目标；快照和等级预览保留两侧筛选条件。

`ChargeRandomOtherAlliedElementCardEffectDefinition.ExcludeEventCard`可在匹配己方卡牌发动的回响中排除本次发动者；默认关闭，保留原有排除效果来源的行为。候选仅限己方存活战场、指定元素且有主动剩余冷却的卡牌，无候选不消耗随机数。回响充能至零产生的主动发动照常支付魔法、重置冷却与执行多重，但该发动及其多重不再触发回响；之后独立正常发动恢复回响。

`IncreaseLeftmostPoisonCardOnSellDefinition`在成功出售后，为己方战场最左侧实际施加中毒的卡牌累加永久`PoisonBonus`；包含固定量、属性量、百分比、回响与已解锁能力，跳过备战区和出售来源。无目标时正常出售；加成在百分比取整后累加一次，再应用发动狂暴，升级保留，旧战斗与回放不受影响。出售前校验加成及施加量溢出。

`ReduceOtherHeroCardCooldownAuraEffectDefinition` 按战斗输入冻结的当前英雄归属筛选己方存活战场卡牌，包含无阵营，排除同英雄归属、敌方、备战与已摧毁卡牌。与现有元素冷却百分比光环累计，合计最多100%，再乘永久冷却倍率；来源失效时撤销精确贡献，不回写对局实例。召唤与转变沿用新身份的归属。

`PassiveOnPickup` 只在成功获得卡牌后结算，不进入战斗队列，允许使用 `GrantRandomOtherHeroSourceLevelCardEffectDefinition` 配置单一尺寸或第二个可选尺寸。候选按key稳定排序并从尺寸并集中等概率抽选，包含无阵营、排除当前英雄归属且须支持来源等级。新建或合并都触发一次，奖励使用合并后的来源等级；先完成来源放置，再执行拾取，满盘保留待领，空池不消耗随机数。连锁拾取沿用统一获得与合并，遭遇的随机选择与拾取共用同一对局随机状态。

`GrantRandomTaggedSourceLevelCardEffectDefinition` 复用上述拾取结算，按指定尺寸与标签筛选所有归属的卡牌，候选须支持来源当前等级；不排除当前英雄归属。奖励使用来源同级，成功合并后按合并等级抽选一次，失败获得不触发。

拾取任务用 `CardAcquiredQuestEvent` 与 `AcquiredElementCardQuestConditionDefinition` 表达，统一由获得／合并入口发布。任务的 `AdditionalActiveEffects` 追加到原发动；`CardAbilityComposer` 为展示与战斗合成当前解锁效果和冷却属性加成，不创建独立冷却或魔法消耗。成长数值放入每级战斗属性，追加效果读取对应属性，因此升级保留进度并更新效果数值。

出售任务使用 `CardSoldQuestEvent` 和 `SoldTaggedCardQuestConditionDefinition`：成功出售后读取被出售实例的实际标签，每张卡牌只推进一次匹配任务，不按等级或标签数量重复计数。来源须在战场区或备战区，从该实例获得后开始累计，不补计历史出售；失败、重复出售及合并消耗不派发出售事件。先移除出售来源、提交出售成长及任务进度，再处理该卡的出售奖励，新生成的任务卡不计入生成它的本次出售。完成后沿用任务能力重算，冷却缩减只在来源位于战场区时生效；升级保留进度与永久成长。

`SourceCardActivationQuestConditionDefinition` 累计来源自己的成功主动发动，包含多重。战斗输入冻结任务定义和已有进度，`BattleQuestResolver` 在发动结束后推进并即时应用已支持属性奖励、追加发动效果、`UnlockedElementKeys` 和 `UnlockedTags`；回放读取冻结进度、元素、标签及能力。当前组多重不追补，新效果从后续发动生效。战后 `CardQuestService.ApplyBattleProgress` 只写回己方真实实例的冻结进度，并按已有机制重算任务属性贡献；身份奖励创建新的只读身份对象，不修改共享 Definition，升级保留进度与身份。

每个具体内容保持独立文件，由 `DefinitionRegistry.Scan` 发现公开、非抽象、无参定义。验证夹具保持 internal，不进入正式内容池。共享构造辅助只在有实际复用时建立。

- 卡牌：身份、初始等级、额外标签、插画与描述从定义正确传入实例和快照；尺寸标签自动推导。全部支持等级都应能实例化，并验证实际效果。
- 英雄：身份、称号、收入和战斗初值一致；`HeroIdentityAttributes.Illustration` 保存项目内原画的只读 `StringName` 资源标识，并传入 `HeroSnapshot`，用于选角预览和英雄面板。原画保存在 `art/ui/heroes/`；不将怪物列入可选英雄。
- 技能：身份、初始等级和支持等级一致；仅用被动能力，并验证触发次数和目标。
- 导师：独立注册、只读身份、筛选规则、等级传递、确定性候选及领取隔离；正式内容不得混入内部验证夹具。
- 遭遇：key、类型、轮次、权重、等级、选项 key/名称一致；同一选项只属于一个展示槽。选项 key 在所属遭遇中唯一。
- 套装：卡牌最多一个归属；计数、目标、阈值叠加和生命周期遵循游戏规则。`PassiveOnRoundStart`配合`GrantExperienceEffectDefinition`在轮收入尚未结算时授予经验；`PassiveOnBattleVictory`读取开战冻结的已激活阈值，胜利后授予经验。`ModifyHeroLevelScaledAttributeEffectDefinition`在持续己方英雄能力中按当前等级乘系数提供Modifier，棋盘变化、回合经验、轮经验或战后经验升级后重算，失效撤销精确贡献。套装阈值描述从能力投影，正式数值只维护在`scripts/Content/Sets/`。

内容注册与实例创建均直接使用 Definition；具体数值、词条说明和能力组合只在对应内容文件维护。

英雄升级词条使用`keyword.upgrade`与`PassiveOnHeroLevelUp`。各应用层经验渠道统一调用`HeroExperienceService.Grant`；一次经验连续升多级时逐级结算冻结来源。`SocketRandomSourceGemEffectDefinition`为来源第一个空孔随机镶嵌宝石，允许备战时由`AllowsBench`指定；从创建对局时注入的正式宝石池按key稳定排序等概率选择，允许重复，空池／孔满／无可镶嵌目标不消耗随机数。既有孔位不替换，卡牌合并不触发英雄升级；旧战斗输入与快照保持冻结。

`GrantValuePercentExperienceOnSellDefinition`配置出售经验占最终价值的百分比（1～100），由出售用例向下取整后交给统一英雄经验结算；与常规金币回补同时执行，不从基础价值计算。提交前使用只读经验预检，校验升级等级、最大生命和最大魔法的溢出；英雄升级后的持续属性通过现有棋盘刷新重算。该奖励可在Definition或等级配置中复用，UI读取同一奖励定义生成出售说明。


### 商店筛选与遭遇资格

`CardShopEncounterDefinition.RequiredSize`为空时不限制卡牌尺寸；`OnlyHeroFaction`默认true，仅允许当前英雄归属，设为false时跨所有归属（含无阵营）；`RequiredTag`可选地限定卡牌必须具备的标签。型号商店沿用原有尺寸与归属限制。首批商品、单件抽取及付费刷新共享同一合格池和等级／满级排除规则，不按具体遭遇key分支。
`EncounterIdentityAttributes.AllowedHeroKeys`是只读英雄key白名单；不传或空列表表示所有英雄，非空列表仅允许所列英雄（无英雄时不合格）。构造时复制并冻结，拒绝空key及重复项；注册时校验每个引用英雄存在。`EncounterDefinition.CanAppearFor`统一按英雄身份key判断，不需要子类覆写。普通、固定怪物和PvP排程均在抽选前调用，和商店商品池的跨职业条件独立。新增受限遭遇只需在自身Definition配置该列表。具体英雄、等级、轮次及权重只维护在对应Definition。

### 元素计数、有效标签与额外发动

`HealEffectDefinition.PerAlliedElementKey` 可按己方存活战场指定元素卡牌数量乘以治疗系数，包含来源自身，排除备战、敌方和已摧毁卡牌；治疗加成在乘积之后累加。卡面读取冻结最终治疗，详情保留公式与加成。

`GrantTagToAlliedElementCardsEffectDefinition` 为持续光环，按元素向己方存活战场卡牌提供有效标签，不修改实例原始标签。筛选、标签计数和回放读取统一有效标签；来源失效只撤销自身贡献，多来源及原生标签保留。`ReduceAlliedElementCardCooldownAuraEffectDefinition.RequiredTag` 可进一步限定有效标签。

`TriggerOtherAlliedElementActivesEffectDefinition` 按稳定战场顺序额外发动己方其他存活战场指定元素卡牌的主动能力，跳过魔法与剩余冷却检查，不修改目标原冷却，包含多重。一次触发链共享已发动卡牌集合，来源先加入集合，每张目标卡牌最多被额外发动一次；延迟多重继承同一集合。

亡语使用 `keyword.deathrattle` 与 `EchoOnSourceDestroyed`，仅在战场来源真实被摧毁后结算一次，普通及永久摧毁均触发；摧毁仍取消原有排队和多重。`ActivateSourceCardEffectDefinition` 可组合为发动来源主动效果，只有亡语明确排入的能力允许已摧毁来源执行；不恢复卡牌、光环或正常冷却发动。

`CardShopEncounterDefinition.RequiredElement` 可限制商品元素，与尺寸、标签和职业条件取交集；双元素卡包含目标元素即可。未配置时不限制元素，商品池、单件抽取和刷新共用此筛选。英雄刷新资格仍使用 `AllowedHeroKeys`，与跨职业商品池独立。

`IncreaseHeroMaxHealthBySourceValueEffectDefinition` 在战场来源有效时，按开战冻结的来源价值乘倍率提供己方英雄最大生命光环。多个来源累计，当前生命按贡献差值同步增减并保留已损失生命，撤销时最低保留1生命，不复活已死亡英雄；备战或摧毁来源不生效，旧战斗不受后续价值成长影响。可作为任务解锁能力复用。
