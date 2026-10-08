using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Presentation.Playtest;
using Project_Star.Presentation.Verification;

namespace Project_Star.Presentation;

// Godot 验证入口：组装模块检查列表、运行并显示结果（表现层）。
public sealed partial class PhaseOneVerification : Control
{
    // 验证界面中的分类与检查入口，不保存规则数据。
    private sealed record VerificationGroup(
        string Name,
        IReadOnlyList<(string Name, Func<bool> Check)> Checks);

    private readonly List<VerificationGroup> _groups =
    [
        new("UI 数据与流程", [
            ("图鉴包含全部正式卡牌及支持等级，不创建实体或触发获取", CardCatalogChecks.Definitions),
            ("战斗快照覆盖治疗、状态、冷却、充能、多重与摧毁", BattlePlaybackVerification.CapturedStates),
            ("回放暂停、倍速与跳过结果一致且冻结旧画面", BattlePlaybackVerification.ControlsAndIsolation),
            ("展示快照隔离实例变化并与出售现值一致", PlaytestVerification.SnapshotIsolation),
            ("卡面保留治疗/百分比语义并按插画字段加载", PlaytestVerification.EffectSemanticsAndArtwork),
            ("卡牌详情隐藏无关攻击/冷却并保留真实零值攻击", PlaytestVerification.RelevantDetailAttributes),
            ("插画从定义传入实例、快照和卡面，未配图使用占位", PlaytestVerification.FormalIllustrations),
            ("遭遇与怪物独立原画贯穿快照，商店等级在页面切换后正确保留", PlaytestVerification.EncounterArtwork),
            ("全部卡牌词条描述贯穿定义、等级、实例、快照与详情", PlaytestVerification.FormalDescriptions),
            ("重复刷新不重抽且旧商品不能重复交易", PlaytestVerification.RefreshAndStaleOffers),
            ("事件获得卡牌在同次刷新中进入棋盘且不重复领取", PlaytestVerification.EventAcquisitionRefresh),
            ("校场实例加成在同次刷新中进入卡面", PlaytestVerification.EventModifierRefresh),
            ("怪物与PvP敌方同步可见且结算与重开无残留", PlaytestVerification.EnemyVisibilityAndReset),
        ]),
        new("基础规则与定义", [
            ("0.2 秒等于 2 个战斗 Tick", DefinitionChecks.CheckTickConversion),
            ("非固定步长时间会被拒绝", DefinitionChecks.CheckInvalidTickConversion),
            ("相同 Seed 产生相同随机序列", DefinitionChecks.CheckDeterministicRandom),
            ("保存随机状态后可以继续序列", DefinitionChecks.CheckRandomResume),
            ("成功 Result 正确携带数据", DefinitionChecks.CheckSuccessfulResult),
            ("失败 Result 使用 StringName 错误码", DefinitionChecks.CheckFailureCode),
            ("身份字段创建后保持只读", DefinitionChecks.CheckReadonlyIdentity),
            ("General 是合法的普通属性", DefinitionChecks.CheckGeneralElement),
            ("双属性按规范顺序保存", DefinitionChecks.CheckElementNormalization),
            ("非法属性组合会被拒绝", DefinitionChecks.CheckInvalidElements),
            ("多来源 Modifier 自然累加", DefinitionChecks.CheckModifierStacking),
            ("移除 Modifier 不影响其他来源", DefinitionChecks.CheckModifierRemoval),
            ("尺寸自动生成型号标签和占格数", DefinitionChecks.CheckSizeTag),
            ("未知标签显示原始 Key", DefinitionChecks.CheckUnknownTagDisplay),
            ("卡牌定义组合身份、属性与标签", DefinitionChecks.CheckCardDefinition),
            ("注册表登记英雄、卡牌与遭遇定义", DefinitionChecks.CheckDefinitionDiscovery),
            ("莫娜与帕拉帝恩独立注册且称号传入对局快照", DefinitionChecks.CheckFormalHeroes),
            ("注册表拒绝同类型重复 Key", DefinitionChecks.CheckDuplicateDefinition),
            ("套装注册表拒绝未知套装归属", DefinitionChecks.CheckUnknownCardSet),
            ("定义中的初始属性不可修改", DefinitionChecks.CheckFrozenDefinition),
        ]),
        new("对局与卡牌经济", [
            ("每回合经验幂等、每10经验升级及怪物奖励和重开正确", HeroExperienceChecks.TurnsAndThresholds),
            ("工厂创建完全独立的卡牌实例", EconomyChecks.CheckIndependentInstances),
            ("新对局可以选择英雄并生成卡牌", EconomyChecks.CheckMatchCreation),
            ("不同对局之间不共享状态", EconomyChecks.CheckSessionIsolation),
            ("每轮开始按英雄收入幂等发放金钱", EconomyChecks.CheckRoundIncome),
            ("尺寸与等级按翻倍表计算初始价值", EconomyChecks.CheckInitialValues),
            ("特殊卡牌可以覆写价值系数", EconomyChecks.CheckSpecialValueCoefficient),
            ("获得卡牌统一设置半价现值", EconomyChecks.CheckAcquisitionSources),
            ("半价出现小数时向下取整", EconomyChecks.CheckAcquiredValueRounding),
            ("等级变化不会重算当前价值", EconomyChecks.CheckLevelDoesNotRecalculateValue),
            ("同名同级卡牌连续合并且保留目标实例", EconomyChecks.CheckCardMergeUpgrade),
            ("卡牌可以限制等级并应用分级配置", EconomyChecks.CheckCardLevelDefinitions),
            ("无阵营无属性卡牌支持分级价值且没有能力", EconomyChecks.CheckBeastHideDefinition),
            ("钻石固定为4级且获得后价值增加20", EconomyChecks.CheckDiamondDefinition),
            ("珠宝袋出售后自动获得同级材料卡", EconomyChecks.CheckJewelryBagSaleReward),
            ("百宝箱出售后获得三件同级小型材料", EconomyChecks.CheckTreasureChestSaleReward),
            ("型号商店只提供当前英雄归属的对应尺寸卡牌", EconomyChecks.CheckSizeShopCardPools),
            ("商店商品不超过商店等级，首批及刷新共用上限且正确处理空池", EconomyChecks.CheckShopLevelLimit),
            ("购买按报价扣款并登记卡牌归属", EconomyChecks.CheckPurchase),
            ("余额不足时交易无任何修改", EconomyChecks.CheckInsufficientWealth),
            ("出售按现值回补且只能一次", EconomyChecks.CheckSale),
        ]),
        new("棋盘、套装与任务", [
            ("卡牌可以直接放入空闲格", BoardChecks.CheckDirectPlacement),
            ("左右方向都能形成推挤方案", BoardChecks.CheckPushDirections),
            ("距离和影响数相同时优先右推", BoardChecks.CheckRightTieBreak),
            ("不可推挤卡牌会阻止放置", BoardChecks.CheckUnpushableCard),
            ("同盘移动会先释放自身原位", BoardChecks.CheckSameZoneMove),
            ("跨区失败时两个区域均不改变", BoardChecks.CheckCrossZoneRollback),
            ("同一实例只存在于一个棋盘区域", BoardChecks.CheckUniqueBoardLocation),
            ("卡牌不能越过棋盘容量边界", BoardChecks.CheckBoardCapacity),
            ("套装按战场不同卡牌累计阈值并精确移除", BoardChecks.CheckCardSetBonuses),
            ("显式套装目标可覆盖战场卡牌与英雄但不覆盖备战区", BoardChecks.CheckCardSetExplicitTargets),
            ("套装战斗来源在成员摧毁后仍按快照触发", BoardChecks.CheckCardSetBattleSource),
            ("任务按卡牌实例累计且仅在战场区激活解锁能力", BoardChecks.CheckCardQuestProgress),
            ("任务解锁的战斗能力随来源卡牌摧毁而失效", BoardChecks.CheckCardQuestBattleDestroy),
            ("移出棋盘后卡牌可以出售", BoardChecks.CheckRemoveThenSell),
        ]),
        new("遭遇与轮次结算", [
            ("大主教从1级传授圣骑士技能，排除其他归属并随访问等级成长", MentorChecks.Archbishop),
            ("导师独立注册且遭遇引用校验拒绝未知导师", MentorChecks.Definitions),
            ("导师按不同规则筛选并按自身等级授予技能", MentorChecks.FilteringAndLevels),
            ("导师候选确定且不重复，空池不推进随机，旧快照冻结", MentorChecks.DeterministicChoices),
            ("导师领取沿用合并并拒绝重复、跨局、过期和终局操作", MentorChecks.ClaimAndIsolation),
            ("导师遭遇等级传入选择和技能，刷新及重开不重复领取", MentorChecks.EncounterFlow),
            ("野猪使用默认属性并携带三张1级卡牌和1级冲撞", EncounterChecks.CheckBoarMonsterDefinition),
            ("校场按定义永久提升英雄生命或战场卡牌攻击", EncounterChecks.CheckTrainingGround),
            ("垃圾场固定展示零钱与材料选项并结算奖励", EncounterChecks.CheckLandfill),
            ("酒馆限时出现并结算交易与下一场战斗加成", EncounterChecks.CheckTavernEncounter),
            ("普通回合三选一且包含商店", EncounterChecks.CheckNormalEncounterChoices),
            ("相同 Seed 生成相同遭遇候选", EncounterChecks.CheckDeterministicEncounters),
            ("候选生成时立即记录为已出现", EncounterChecks.CheckEncounterSeenHistory),
            ("第 4 回合固定三个怪物遭遇", EncounterChecks.CheckMonsterTurn),
            ("第 8 回合固定 PvP 并推进轮次", EncounterChecks.CheckPvpTurnAdvance),
            ("新对局不会继承遭遇历史", EncounterChecks.CheckEncounterHistoryIsolation),
            ("怪物战失败按损失生命比例奖励金币经验且不扣声望", EncounterChecks.CheckMonsterLoss),
            ("怪物战胜利按怪物等级奖励并抽取待领取卡牌", EncounterChecks.CheckMonsterReward),
            ("怪物技能可作为战利品领取", EncounterChecks.CheckMonsterSkillReward),
            ("棋盘已满时怪物卡牌奖励保持待领取", EncounterChecks.CheckMonsterRewardRetention),
            ("PvP 失败按战斗轮数扣声望", EncounterChecks.CheckPvpReputationLoss),
            ("十次 PvP 胜利结束对局", EncounterChecks.CheckTenPvpWins),
            ("胜利与声望归零同时满足时胜利优先", EncounterChecks.CheckVictoryPriority),
            ("永久摧毁同时移除归属记录和棋盘实例", EncounterChecks.CheckPermanentChangeApplication),
            ("对局快照保留只读结算数据", EncounterChecks.CheckMatchSnapshot),
        ]),
        new("战斗与卡牌能力", [
            ("战斗快照与对局实例隔离", CombatChecks.CheckBattleSnapshotIsolation),
            ("相同输入产生相同战斗日志", CombatChecks.CheckDeterministicBattle),
            ("首次攻击在完整冷却后发动", CombatChecks.CheckFirstActivationTiming),
            ("同 Tick 按玩家方优先进入 FIFO", CombatChecks.CheckStableFifoOrder),
            ("伤害先扣护甲再扣生命", CombatChecks.CheckArmorDamage),
            ("最大生命百分比伤害向下取整并先扣护甲", CombatChecks.CheckMaxHealthPercentDamage),
            ("野猪按己方英雄当前生命比例造成分级伤害", CardCombatChecks.CheckBoarCard),
            ("轻骑兵伤害、成长冷却与人类攻击强化正确结算", CardCombatChecks.CheckLightCavalry),
            ("臂铠按卡牌多重属性额外发动并重复获得护甲", CardCombatChecks.CheckArmguard),
            ("荆棘甲先获得护甲再按英雄当前护甲造成伤害", CardCombatChecks.CheckThornArmor),
            ("魔能盾按己方累计魔法消耗获得护甲", CardCombatChecks.CheckArcaneShield),
            ("军靴使相邻卡牌疾速且对人类翻倍", CardCombatChecks.CheckMilitaryBoots),
            ("神圣狮鹫使相邻己方卡牌疾速并强化获疾速的人类", CardCombatChecks.CheckHolyGriffin),
            ("神圣狮鹫使全部相邻己方卡牌疾速并随机带一张人类飞行", GriffinFlightChecks.TargetsAndReplay),
            ("修女治疗己方英雄并充能另一件光属性卡牌", CardCombatChecks.CheckNun),
            ("大教堂光环为己方光属性卡牌提供可移除的多重", CardCombatChecks.CheckCathedral),
            ("铁匠铺强化装备已有的攻击与护甲能力", CardCombatChecks.CheckBlacksmith),
            ("风之刃分级伤害、自身及多重发动回响按1/2/4/8增长", WindBladeChecks.LevelsAndEchoes),
            ("风之刃多重回放冻结且不带入下场战斗", WindBladeChecks.PlaybackAndReset),
            ("黎明之剑随机摧毁邪恶卡牌并按双方摧毁数倍增攻击", CardCombatChecks.CheckHolySlashingBlade),
            ("教团远征军赋予临时恶魔标签并按存活恶魔增加伤害", CardCombatChecks.CheckOrderCrusader),
            ("无攻击来源时由日蚀结束战斗", CombatChecks.CheckBattleTimeout),
            ("魔法不足时不扣魔法也不发动", CombatChecks.CheckInsufficientMana),
            ("回响产生的事件不会触发其他回响", CombatChecks.CheckEchoDoesNotChain),
            ("灼伤每0.6秒先伤害后减1，中毒每秒穿甲且叠加不衰减", CombatChecks.CheckBurnAndPoison),
            ("永久摧毁只生成永久变化记录", CombatChecks.CheckPermanentDestroy),
        ]),
        new("技能", [
            ("技能使用独立实例并按同级规则合并", SkillChecks.CheckSkillMerge),
            ("空战场的被动技能可触发且回响不会连锁", SkillChecks.CheckSkillBattle),
            ("冲撞仅在己方首张卡牌发动后触发一次并按等级结算", SkillChecks.CheckChargeSkill),
            ("捍卫各等级在战斗开始时累加己方英雄等级护甲", SkillChecks.CheckDefendSkill),
            ("大主教传授捍卫并合并，护甲叠加且战斗输入隔离", SkillChecks.CheckDefendMentorAndIsolation),
            ("至圣斩仅支持4级，大主教按等级筛选并传授", DivineSmiteChecks.DefinitionAndMentor),
            ("至圣斩仅放大己方光属性攻击并按敌方标签判断", DivineSmiteChecks.FilteringAndDamage),
            ("至圣斩响应摧毁、临时标签、转变和召唤变化", DivineSmiteChecks.DynamicConditions),
            ("至圣斩叠加攻击加成和多个来源且配置冻结", DivineSmiteChecks.StackingAndValidation),
        ]),
    ];

    public override void _Ready()
    {
        _groups.Add(new VerificationGroup("内容词条边界", [
            ("词条身份只读隔离、合法名称及输入边界", ContentDataChecks.DescriptionEntryBoundaries),
        ]));
        _groups.Add(new VerificationGroup("扩展边界与战斗记录", [
            ("飞行/狂暴为布尔状态，重复设置幂等且每场重置", CardStateChecks.BooleanLifecycle),
            ("飞天扫帚分级冷却、进入飞行与1秒疾速正确", FlyingBroomChecks.LevelsAndActivation),
            ("咩咩羊分级疾速、变羊魔棒分级冷却及注册引用正确", PolymorphChecks.LevelsAndHaste),
            ("转变随机筛选、等级保留、完整冷却与旧能力状态清除", PolymorphChecks.TargetsAndReset),
            ("变羊回放身份冻结且下场恢复原卡与位置等级", PolymorphChecks.PlaybackAndRestoration),
            ("小型生命药水分级治疗后仅本场摧毁并在下场恢复", SmallRedPotionChecks.HealingAndConsumption),
            ("小型魔法药水恢复魔法并在恢复后本场摧毁，上限及多重正确", SmallManaPotionChecks.RestoreAndConsume),
            ("炼金釜出售消耗品永久成长、冻结快照和溢出预检正确", AlchemyCauldronChecks.SaleGrowthAndBattle),
            ("原木法杖拾取含合并、备战累计、统一发动与升级正确", LogStaffChecks.AcquisitionAndActivation),
            ("黑犀金龟等级、随机迟缓目标、叠加及飞行减时长正确", BlackRhinocerosBeetleChecks.LevelsAndSlow),
            ("破誓者分级随机不重复迟缓、双方光牌动态冷却及倍率正确", OathbreakerChecks.TargetsAndAura),
            ("钉头页锤分级伤害、护甲吸收与备战排除正确", FlangedMaceChecks.LevelsAndArmor),
            ("钉头页锤实时护甲倍率、攻击加成、多重与狂暴正确", FlangedMaceChecks.DynamicArmorAndBonuses),
            ("骑佩短剑分级回响伤害、完整占格相邻与多重正确", RidingShortswordChecks.LevelsAndAdjacency),
            ("骑佩短剑按攻击卡牌发动筛选，排除辅助、敌方及备战", RidingShortswordChecks.AttackFilters),
            ("骑佩短剑无主动发动、来源失效及回响截断正确", RidingShortswordChecks.EchoLifetime),
            ("圣殿骑士分级攻击、两侧同级召唤与短剑回响正确", TemplarKnightChecks.LevelsAndEchoes),
            ("相邻召唤边界、阻挡、争用顺序及完整占格正确", TemplarKnightChecks.PositionsAndOrdering),
            ("召唤来源摧毁后独立、永久摧毁无库存变化及注册校验正确", TemplarKnightChecks.LifecycleAndValidation),
            ("召唤物双边回放身份位置冻结、播放控制及战后恢复正确", TemplarKnightChecks.PlaybackAndRestoration),
            ("出售从左查找战场土属性目标，倍率永久累乘且升级保留", BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence),
            ("小数冷却倍率用于首发和重置，兼容增时长、疾速及充能", BlackRhinocerosBeetleChecks.MultiplierBattleTiming),
            ("飞行回响准确定位己方事件卡牌、叠加且不连锁", FlyingBroomChecks.EventTargetsAndStacking),
            ("飞行减半新增迟缓/禁锢，兼容相邻倍率且不影响疾速", CardStateChecks.FlyingDurations),
            ("狂暴覆盖全部伤害公式及中毒/灼伤并在护甲前取整", CardStateChecks.BerserkDamageAndStatus),
            ("狂暴仅加成发动与多重，不重复加成被动或周期伤害", CardStateChecks.BerserkActiveOnly),
            ("怪物与 PvP 使用注入的对手来源", PlaytestVerification.OpponentProviderInjection),
            ("固定战斗的结果、事件和回放状态保留基线", CombatTraceChecks.PreservesTraces),
            ("三种旗帜身份、分级光环与攻击护甲治疗实际效果", StandardAuraChecks.LevelsAndEffects),
            ("旗帜叠加、摧毁贡献扣除、敌方备战排除与冻结治疗显示", StandardAuraChecks.StackingAndRemoval),
            ("圣殿骑士召唤物立即获得战意旗帜光环", StandardAuraChecks.SummonsReceiveAura),
            ("军团旗帜手分级随机人类强化、筛选与战斗累加", StandardAuraChecks.StandardBearerBuff),
            ("军团旗帜手同级随机旗帜召唤、边界占位与确定性", StandardAuraChecks.StandardBearerSummon),
            ("秩序板甲分级护甲、随机单张迟缓、累加与无目标发动", OrderPlateArmorChecks.ArmorAndSlow),
            ("光辉旗帜分级冷却、光属性筛选与冻结回放", RadiantStandardChecks.LevelsAndFiltering),
            ("光辉旗帜百分比叠加、来源失效与永久倍率组合", RadiantStandardChecks.StackingRemovalAndMultiplier),
            ("麦田分级、全体人类充能、即时发动及多重回响", WheatFieldChecks.LevelsAndCharge),
            ("麦田人类疾速回响加速冷却与目标过滤", WheatFieldChecks.EchoAcceleratesCooldown),
            ("传动齿轮两种身份、分级、原画与多重累加正确", TransmissionGearChecks.LevelsAndEffects),
            ("传动齿轮左右方向、空格隔断、来源失效与回响截断", TransmissionGearChecks.DirectionAndLifetime),
            ("传动齿轮疾速加速、充能即时发动与队列去重", TransmissionGearChecks.CooldownAndQueue),
            ("单侧相邻回响与充能拒绝无效配置及非卡牌来源", TransmissionGearChecks.ConfigurationBoundaries),
            ("羽饰头盔身份、原画、2至4级伤害及攻击加成正确", PlumedHelmetChecks.LevelsAndDamage),
            ("羽饰头盔双侧人类发动、多重充能及相邻筛选正确", PlumedHelmetChecks.AdjacentHumanFilters),
            ("羽饰头盔充能即时发动、队列去重与冻结配置正确", PlumedHelmetChecks.ChargeTimingAndBoundaries),
            ("极云四张控制卡身份、等级、原画及冻结能力正确", JiyunControlChecks.IdentityAndLevels),
            ("酒葫芦分级治疗、己方随机迟缓、飞行、多重及衰减", JiyunControlChecks.GourdHealingAndSlow),
            ("斗笠分级护甲、施加方筛选及迟缓充能即时发动", JiyunControlChecks.HatArmorAndCharge),
            ("隐秘竹林木属性筛选、分级迟缓及回响截断", JiyunControlChecks.GroveFilteringAndEchoCutoff),
            ("禅光寺双方攻击分类、事件目标、禁锢叠加及衰减", JiyunControlChecks.TempleTargetsAndDuration),
            ("极云通用迟缓和事件卡牌机制拒绝无效配置", JiyunControlChecks.ConfigurationBoundaries),
            ("登神者分级、3秒发动及20/60/120次任务即时解锁", AscendantChecks.LevelsAndThresholds),
            ("登神者跨战斗永久成长、升级及备战身份保留", AscendantChecks.CrossBattleAndUpgrade),
            ("登神者购买及连续合并保留最高等级实例、任务和永久成长", AscendantChecks.MergePreservesProgress),
            ("登神者自身发动筛选、实例隔离及失败战斗成长", AscendantChecks.FiltersAndLoss),
            ("登神者任务身份回放、多重阈值及有效攻击百分比灼伤", AscendantChecks.ReplayAndPercentStatus),
            ("登神者召唤/转变仅本场成长与百分比配置验证", AscendantChecks.TemporarySourcesAndValidation),
        ]));
        _groups.Add(new VerificationGroup("试玩场景集成", [
            ("局外图鉴搜索、归属、等级、空结果、旧按钮与返回选角正确", () => CardCatalogChecks.Interaction(this)),
            ("导师技能选项真实渲染、完整提示与旧按钮解绑正确", () => MentorChecks.RenderedChoices(this)),
            ("飞行/狂暴快照、回放投影与真实状态显示一致", () => CardStateChecks.PlaybackAndDisplay(this)),
            ("上方拖拽出售贯穿真实入口、奖励、失效与战斗限制", () => DragSaleChecks.Transactions(this)),
            ("真实场景事件刷新、敌方可见性与重开事件连接", () => PlaytestVerification.RenderedScene(this)),
            ("三尺寸卡牌贴合棋盘，上下栏等高，缩放不改变快照", () => PlaytestVerification.CardAndSlotGeometry(this)),
            ("独立组件渲染无命令、页面互斥与旧按钮解绑", () => PlaytestVerification.IsolatedComponents(this)),
            ("棋盘预览无副作用并在提交时重新验证阻挡", PlaytestVerification.BoardPreviewAndCommit),
            ("拖拽偏移、跨区目标、只读区域与过期对局", () => PlaytestVerification.DragTargets(this)),
            ("拖拽查询、取消选择与旧对局隔离", PlaytestVerification.DragPresenterState),
            ("完整购买失败无残留、满盘合并与重复购买", PlaytestVerification.CompletePurchases),
            ("连续合并释放空间并放置原库存目标", PlaytestVerification.MergePlacementReleasedSpace),
            ("棋盘出售失败保留、现值回补与出售奖励", PlaytestVerification.CompleteSales),
            ("指定技能奖励、过期领取与出售确认隔离", PlaytestVerification.RewardSelectionAndStaleSale),
            ("满盘卡牌奖励保留且可选择后续技能", PlaytestVerification.FullBoardRewardSelection),
            ("真实卡面购买、出售确认、奖励浮层与重开", () => PlaytestVerification.TransactionScene(this)),
            ("真实遭遇原画图卡选择、商店晶体与旧按钮解绑", () => PlaytestVerification.EncounterArtworkScene(this)),
            ("测试关卡各类遭遇默认5级，正式排程等级保持不变", () => PlaytestVerification.TestEncounterLevels(this)),
            ("宝石空孔、镶嵌校验、合并出售、冻结快照与真实卡面正确", () => GemSocketChecks.Lifecycle(this)),
            ("四位英雄初始属性、插画身份、选角缩略图与头像正确", () => HeroArtworkChecks.Check(this)),
        ]));
        var categoryButtons = GetNode<HFlowContainer>("Margin/Panel/Margin/Content/Categories");
        var allChecks = _groups.SelectMany(group => group.Checks).ToArray();
        var allButton = new Button
        {
            Text = $"全部验证 ({allChecks.Length})",
            CustomMinimumSize = new Vector2(150, 42),
        };
        categoryButtons.AddChild(allButton);
        allButton.Pressed += () => RunChecks("全部验证", allChecks);

        foreach (var group in _groups)
        {
            var button = new Button
            {
                Text = $"{group.Name} ({group.Checks.Count})",
                CustomMinimumSize = new Vector2(170, 42),
            };
            categoryButtons.AddChild(button);
            button.Pressed += () => RunChecks(group.Name, group.Checks);
        }

        GetNode<Label>("Margin/Panel/Margin/Content/Title").Text = "选择验证范围";
        GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text =
            $"当前共 {allChecks.Length} 项，选择上方按钮运行全部验证或指定分类。";
        GetNode<Button>("Margin/Panel/Margin/Content/Back").Pressed +=
            () => GetTree().ChangeSceneToFile("res://Playtest.tscn");
        if (OS.GetCmdlineUserArgs().Contains("--capture-catalog"))
            Callable.From((Action)(async () =>
            {
                try { await CardCatalogChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mentors"))
            Callable.From((Action)(async () =>
            {
                try { await MentorChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify"))
            Callable.From(() =>
            {
                RunChecks("全部验证", allChecks);
                var output = GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text;
                GetTree().Quit(output.Contains("✗") ? 1 : 0);
            }).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-native-drag"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    var passed = await PlaytestVerification.NativeDrag(this) && await DragSaleChecks.Native(this);
                    GD.Print($"原生拖拽输入验证：{(passed ? "通过" : "失败")}");
                    GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-ui-windows"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    var passed = await PlaytestVerification.WindowsAndKeyboard(this);
                    GD.Print($"窗口、焦点与键盘移动验证：{(passed ? "通过" : "失败")}");
                    GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
    }

    private void RunChecks(
        string groupName,
        IReadOnlyList<(string Name, Func<bool> Check)> checks)
    {
        var results = new List<string>(checks.Count);
        var passedCount = 0;
        foreach (var (name, check) in checks)
        {
            try
            {
                var passed = check();
                if (passed) passedCount++;
                results.Add($"{(passed ? "✓" : "✗")} {name}");
            }
            catch (Exception exception)
            {
                results.Add($"✗ {name}\n    {exception.GetType().Name}: {exception.Message}");
            }
        }

        var allPassed = passedCount == checks.Count;
        var title = GetNode<Label>("Margin/Panel/Margin/Content/Title");
        var output = GetNode<Label>("Margin/Panel/Margin/Content/Results/Output");
        title.Text = $"{groupName}：{(allPassed ? "通过" : "失败")} ({passedCount}/{checks.Count})";
        title.Modulate = allPassed ? new Color("75d69c") : new Color("ff7b72");
        output.Text = string.Join("\n", results);
        GD.Print($"{title.Text}\n{output.Text}");
    }

}
