using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        IReadOnlyList<VerificationCheck> Checks);

    // 同步规则检查和需要分帧的原生交互检查共用执行入口。
    private sealed record VerificationCheck(string Name, Func<Task<bool>> Run)
    {
        public VerificationCheck(string name, Func<bool> check) : this(name, () => Task.FromResult(check())) { }
    }

    private sealed record VerificationBatch(StringName Key, string Name, IReadOnlyList<VerificationCheck> Checks);
    private bool _running;
    private bool _runningBatches;
    private int _progressCompleted;
    private int _progressTotal;
    private string _progressBatch = "";

    private void UpdateProgress()
    {
        var bar = GetNode<ProgressBar>("Margin/Panel/Margin/Content/Progress");
        bar.MaxValue = Math.Max(1, _progressTotal);
        bar.Value = _progressCompleted;
        var percent = _progressTotal == 0 ? 0 : _progressCompleted * 100 / _progressTotal;
        GetNode<Label>("Margin/Panel/Margin/Content/ProgressText").Text =
            $"{_progressBatch} · 已完成 {_progressCompleted} / {_progressTotal} 项 · {percent}%";
    }

    private readonly List<VerificationGroup> _groups =
    [
        new("虫蛊袋", [new("虫蛊袋分级、双回响与虫族筛选", GuInsectPouchChecks.Combat)]),
        new("风刃螳螂与静水蝎", [new("两卡分级、随机疾速与伤害", MantisScorpionChecks.LevelsAndCombat), new("两卡出售元素筛选及永久冷却", MantisScorpionChecks.Sales)]),
        new("魔导士套装", [new("技能数量缩放与战斗隔离", MagisterChecks.Combat), new("升级、胜利与技能池", MagisterChecks.Rewards)]),
        new("UI 数据与流程", [
            new("全卡各级描述无英文、无冒号且引用标注完整", CardDescriptionChecks.AllLevels),
            new("卡牌发动目标仅出现必要次数且召唤转变引用明确", CardDescriptionChecks.TargetsAndReferences),
            new("引用卡名完整高亮且不拆分内部元素关键词", CardDescriptionChecks.ReferenceRendering),
            new("全卡护甲等级与宝石预览保留基值、成长及光环", UiPrototypeSyncChecks.PreviewArmor),
            new("图鉴包含全部正式卡牌及支持等级，不创建实体或触发获取", CardCatalogChecks.Definitions),
            new("战斗快照覆盖治疗、状态、冷却、充能、多重与摧毁", BattlePlaybackVerification.CapturedStates),
            new("回放暂停、倍速与跳过结果一致且冻结旧画面", BattlePlaybackVerification.ControlsAndIsolation),
            new("展示快照隔离实例变化并与出售现值一致", PlaytestVerification.SnapshotIsolation),
            new("卡面显示治疗与未知比例数值、详情保留公式并按插画字段加载", PlaytestVerification.EffectSemanticsAndArtwork),
            new("卡牌详情隐藏无关攻击/冷却并保留真实零值攻击", PlaytestVerification.RelevantDetailAttributes),
            new("插画从定义传入实例、快照和卡面，未配图使用占位", PlaytestVerification.FormalIllustrations),
            new("遭遇与怪物独立原画贯穿快照，商店等级在页面切换后正确保留", PlaytestVerification.EncounterArtwork),
            new("全部卡牌词条描述贯穿定义、等级、实例、快照与详情", PlaytestVerification.FormalDescriptions),
            new("重复刷新不重抽且旧商品不能重复交易", PlaytestVerification.RefreshAndStaleOffers),
            new("事件获得卡牌在同次刷新中进入棋盘且不重复领取", PlaytestVerification.EventAcquisitionRefresh),
            new("校场实例加成在同次刷新中进入卡面", PlaytestVerification.EventModifierRefresh),
            new("怪物与PvP敌方同步可见且结算与重开无残留", PlaytestVerification.EnemyVisibilityAndReset),
        ]),
        new("基础规则与定义", [
            new("万象舆图四级地域拾取成长、出售任务、价值生命光环与冻结隔离", MyriadAtlasChecks.GrowthAndQuest),
            new("小天使分级治疗、敌方双标签去重、多重与神圣感知联动", LittleAngelChecks.LevelsAndHealing),
            new("神圣感知分级、开战随机标签与战斗隔离", DivineSenseAndAngelChecks.DivineSense),
            new("正义天使分级、恶魔亡灵多重及神圣感知联动", DivineSenseAndAngelChecks.Angel),
            new("投石车分级、人类计数冷却与完整冷却攻击光环", CatapultChecks.LevelsAndAuras),
            new("重锤兵分级伤害、最大生命与当前生命累加及多重", HeavyHammerInfantryChecks.LevelsAndCombat),
            new("枪矛手四级伤害、相邻人类发动成长与战斗隔离", SpearmanChecks.LevelsAndCombat),
            new("农夫四级冷却与双方随机人类充能、即时发动", FarmerChecks.LevelsAndCharge),
            new("战舰舵手四级冷却、载具疾速、到期与实际加速", WarshipHelmsmanChecks.LevelsAndHaste),
            new("战舰炮手三级伤害、全体载具强化及发动回响", WarshipGunnerChecks.LevelsAndCombat),
            new("战舰炮手多来源累加、目标边界与战斗隔离", WarshipGunnerChecks.FiltersAndLifetime),
            new("战舰舵手随机筛选、空目标、多重及疾速累加", WarshipHelmsmanChecks.FiltersAndStacking),
            new("农夫排除自身、非人类、备战、摧毁与就绪目标，多重及随机确定性", FarmerChecks.FiltersAndMulticast),
            new("破晓长弓分级伤害、累计冷却与全局一秒下限", DawnLongbowChecks.LevelsAndCooldown),
            new("多重每隔0.2秒逐次发动及回响，仅支付一次魔法", DawnLongbowChecks.DelayedMulticast),
            new("延迟多重冻结次数且摧毁、修复与转变取消旧发动", DawnLongbowChecks.CancellationAndFrozenCount),
            new("月影喵四级冷却、相邻人类逐张计数及随机禁锢", MoonshadowCatChecks.LevelsAndCombat),
            new("月影喵筛选、光环撤销、永久倍率与禁锢累加衰减", MoonshadowCatChecks.FiltersAndRemoval),
            new("0.2 秒等于 2 个战斗 Tick", DefinitionChecks.CheckTickConversion),
            new("非固定步长时间会被拒绝", DefinitionChecks.CheckInvalidTickConversion),
            new("相同 Seed 产生相同随机序列", DefinitionChecks.CheckDeterministicRandom),
            new("保存随机状态后可以继续序列", DefinitionChecks.CheckRandomResume),
            new("成功 Result 正确携带数据", DefinitionChecks.CheckSuccessfulResult),
            new("失败 Result 使用 StringName 错误码", DefinitionChecks.CheckFailureCode),
            new("身份字段创建后保持只读", DefinitionChecks.CheckReadonlyIdentity),
            new("General 是合法的普通属性", DefinitionChecks.CheckGeneralElement),
            new("双属性按规范顺序保存", DefinitionChecks.CheckElementNormalization),
            new("非法属性组合会被拒绝", DefinitionChecks.CheckInvalidElements),
            new("多来源 Modifier 自然累加", DefinitionChecks.CheckModifierStacking),
            new("移除 Modifier 不影响其他来源", DefinitionChecks.CheckModifierRemoval),
            new("尺寸自动生成型号标签和占格数", DefinitionChecks.CheckSizeTag),
            new("未知标签显示原始 Key", DefinitionChecks.CheckUnknownTagDisplay),
            new("卡牌定义组合身份、属性与标签", DefinitionChecks.CheckCardDefinition),
            new("注册表登记英雄、卡牌与遭遇定义", DefinitionChecks.CheckDefinitionDiscovery),
            new("正式英雄独立注册且默认资源、称号与战斗初值传入新局快照", DefinitionChecks.CheckFormalHeroes),
            new("注册表拒绝同类型重复 Key", DefinitionChecks.CheckDuplicateDefinition),
            new("套装注册表拒绝未知套装归属", DefinitionChecks.CheckUnknownCardSet),
            new("定义中的初始属性不可修改", DefinitionChecks.CheckFrozenDefinition),
        ]),
        new("对局与卡牌经济", [
            new("每回合经验幂等、每10经验升级及怪物奖励和重开正确", HeroExperienceChecks.TurnsAndThresholds),
            new("工厂创建完全独立的卡牌实例", EconomyChecks.CheckIndependentInstances),
            new("新对局可以选择英雄并生成卡牌", EconomyChecks.CheckMatchCreation),
            new("不同对局之间不共享状态", EconomyChecks.CheckSessionIsolation),
            new("每轮开始按英雄收入幂等发放金钱", EconomyChecks.CheckRoundIncome),
            new("尺寸与等级按翻倍表计算初始价值", EconomyChecks.CheckInitialValues),
            new("特殊卡牌可以覆写价值系数", EconomyChecks.CheckSpecialValueCoefficient),
            new("获得卡牌统一设置半价现值", EconomyChecks.CheckAcquisitionSources),
            new("半价出现小数时向下取整", EconomyChecks.CheckAcquiredValueRounding),
            new("等级变化不会重算当前价值", EconomyChecks.CheckLevelDoesNotRecalculateValue),
            new("合成补等级价值差额并保留成长，连续合并与溢出事务正确", EconomyChecks.CheckMergeValueCompensation),
            new("同名同级卡牌连续合并且保留目标实例", EconomyChecks.CheckCardMergeUpgrade),
            new("卡牌可以限制等级并应用分级配置", EconomyChecks.CheckCardLevelDefinitions),
            new("无阵营无属性卡牌支持分级价值且没有能力", EconomyChecks.CheckBeastHideDefinition),
            new("钻石固定为4级且获得后价值增加20", EconomyChecks.CheckDiamondDefinition),
            new("珠宝袋出售后自动获得同级材料卡", EconomyChecks.CheckJewelryBagSaleReward),
            new("百宝箱出售后获得三件同级小型材料", EconomyChecks.CheckTreasureChestSaleReward),
            new("通用遭遇英雄白名单冻结、引用校验与普通／怪物／PvP排程正确", EquipmentShopEncounterChecks.GenericHeroEligibility),
            new("装备商店仅圣骑士与机械师可遭遇，资格与排程一致", EquipmentShopEncounterChecks.EligibilityAndScheduling),
            new("装备商店全职业全尺寸装备筛选、刷新与跨职业购买正确", EquipmentShopEncounterChecks.PoolRefreshAndPurchase),
            new("型号商店只提供当前英雄归属的对应尺寸卡牌", EconomyChecks.CheckSizeShopCardPools),
            new("商店商品不超过商店等级，首批及刷新共用上限且正确处理空池", EconomyChecks.CheckShopLevelLimit),
            new("购买按报价扣款并登记卡牌归属", EconomyChecks.CheckPurchase),
            new("余额不足时交易无任何修改", EconomyChecks.CheckInsufficientWealth),
            new("出售按现值回补且只能一次", EconomyChecks.CheckSale),
        ]),
        new("棋盘、套装与任务", [
            new("卡牌可以直接放入空闲格", BoardChecks.CheckDirectPlacement),
            new("左右方向都能形成推挤方案", BoardChecks.CheckPushDirections),
            new("距离和影响数相同时优先右推", BoardChecks.CheckRightTieBreak),
            new("不可推挤卡牌会阻止放置", BoardChecks.CheckUnpushableCard),
            new("同盘移动会先释放自身原位", BoardChecks.CheckSameZoneMove),
            new("跨区失败时两个区域均不改变", BoardChecks.CheckCrossZoneRollback),
            new("同一实例只存在于一个棋盘区域", BoardChecks.CheckUniqueBoardLocation),
            new("卡牌不能越过棋盘容量边界", BoardChecks.CheckBoardCapacity),
            new("套装按战场不同卡牌累计阈值并精确移除", BoardChecks.CheckCardSetBonuses),
            new("显式套装目标可覆盖战场卡牌与英雄但不覆盖备战区", BoardChecks.CheckCardSetExplicitTargets),
            new("套装战斗来源在成员摧毁后仍按快照触发", BoardChecks.CheckCardSetBattleSource),
            new("任务按卡牌实例累计且仅在战场区激活解锁能力", BoardChecks.CheckCardQuestProgress),
            new("任务解锁的战斗能力随来源卡牌摧毁而失效", BoardChecks.CheckCardQuestBattleDestroy),
            new("移出棋盘后卡牌可以出售", BoardChecks.CheckRemoveThenSell),
        ]),
        new("遭遇与轮次结算", [
            new("大主教从1级传授圣骑士技能，排除其他归属并随访问等级成长", MentorChecks.Archbishop),
            new("导师独立注册且遭遇引用校验拒绝未知导师", MentorChecks.Definitions),
            new("导师按不同规则筛选并按自身等级授予技能", MentorChecks.FilteringAndLevels),
            new("导师候选确定且不重复，空池不推进随机，旧快照冻结", MentorChecks.DeterministicChoices),
            new("导师领取沿用合并并拒绝重复、跨局、过期和终局操作", MentorChecks.ClaimAndIsolation),
            new("导师遭遇等级传入选择和技能，刷新及重开不重复领取", MentorChecks.EncounterFlow),
            new("野猪2级遭遇从第3轮解锁，卡组与冲撞保持1级", EncounterChecks.CheckBoarMonsterDefinition),
            new("火史莱姆40生命、重复熔岩石阵容与灼伤精通实战正确", FireSlimeMonsterChecks.DefinitionAndBattle),
            new("火史莱姆胜利奖励保留卡牌技能等级且只领取一次", FireSlimeMonsterChecks.Rewards),
            new("咩咩三张卡牌、治愈技能、等级布局与实际发动正确", BaaMonsterChecks.DefinitionAndBattle),
            new("咩咩胜利奖励保留三种卡牌和治愈等级且只领取一次", BaaMonsterChecks.Rewards),
            new("风车菊四级治疗、出售永久生命与咩咩治愈叠加正确", WindmillDaisyChecks.LevelsHealingAndSales),
            new("冰莲两级双方迟缓、收到迟缓回响与双冰莲连锁截断", IceLotusChecks.LevelsAndCombat),
            new("冰莲己敌施加、多重、飞行零时长与备战摧毁筛选", IceLotusChecks.ReceivedStatusAndFilters),
            new("攻城弩车三级七秒伤害与疾速逐次攻击成长", SiegeBallistaChecks.LevelsDamageAndHaste),
            new("攻城弩车状态筛选、回响截断、来源失效与战斗隔离", SiegeBallistaChecks.FiltersAndIsolation),
            new("重骑兵两级双方八秒攻防与每侧装备分别贡献", HeavyCavalryChecks.LevelsAndNeighbors),
            new("重骑兵相邻边界、排除和撤销、原生数值投影冻结", HeavyCavalryChecks.RemovalAndPreview),
            new("狮王盾护甲百分比、取整、周期与卡面数值正确", LionKingShieldChecks.LevelsArmorAndDamage),
            new("狮王盾护甲光环、狂暴多重、失效与百分比边界", LionKingShieldChecks.AuraAndBerserk),
            new("训练木桩四级双方群体攻击、周期和多重累加", TrainingDummyChecks.LevelsAndGroupBuff),
            new("训练营分级人类群体训练、辅助目标、双方与本场隔离", TrainingCampChecks.LevelsAndTraining),
            new("破碎之星身份、周期护甲消耗、五倍伤害及冻结回放", ArmorShatteringMaulChecks.ConsumptionAndDamage),
            new("破碎之星多重宝石、狂暴、护甲补充、失效与隐身边界", ArmorShatteringMaulChecks.OrderingAndBoundaries),
            new("训练营同级小型人类拾取、合并、满盘、空池与随机状态", TrainingCampChecks.PickupAndBoundaries),
            new("训练木桩攻击分类、目标排除、来源失效和本场隔离", TrainingDummyChecks.FiltersAndSourceLoss),
            new("逃遁的破誓者1级60生命、默认属性、卡组排程与实际迟缓正确", JailbreakerChecks.DefinitionAndBattle),
            new("逃遁的破誓者胜利奖励池保留卡牌等级，金币经验和领取正确", JailbreakerChecks.Rewards),
            new("校场按定义永久提升英雄生命或战场卡牌攻击", EncounterChecks.CheckTrainingGround),
            new("垃圾场固定展示零钱与材料选项并结算奖励", EncounterChecks.CheckLandfill),
            new("暮歌丛林固定选项、概率配置与原画正确", DuskSongJungleChecks.Definitions),
            new("探险分类、流沙定义、概率文案、解锁与非法奖励校验", QuicksandExpanseChecks.DefinitionsAndScheduling),
            new("流沙金币全区间、概率边界、同阵营奖励等级与确定性", QuicksandExpanseChecks.OutcomesAndLevels),
            new("丛林概率边界、确定性、奖励等级与满盘结算正确", DuskSongJungleChecks.Outcomes),
            new("丛林怪物分支进入战斗并保持同一回合", DuskSongJungleChecks.Flow),
            new("酒馆限时出现并结算交易与下一场战斗加成", EncounterChecks.CheckTavernEncounter),
            new("普通回合三选一且包含商店", EncounterChecks.CheckNormalEncounterChoices),
            new("遭遇按初始等级在1／3／5／8／10轮解锁并遵守轮次范围", EncounterChecks.CheckEncounterLevelUnlocks),
            new("已解锁遭遇可提升到4级且5级仅来自特殊遭遇", EncounterChecks.CheckEncounterVisitLevelLimits),
            new("遭遇等级在初始至4级等概率抽取且随机状态可复现", EncounterChecks.CheckRandomEncounterLevels),
            new("相同 Seed 生成相同遭遇候选", EncounterChecks.CheckDeterministicEncounters),
            new("候选生成时立即记录为已出现", EncounterChecks.CheckEncounterSeenHistory),
            new("第 4 回合固定三个怪物遭遇", EncounterChecks.CheckMonsterTurn),
            new("第 8 回合固定 PvP 并推进轮次", EncounterChecks.CheckPvpTurnAdvance),
            new("新对局不会继承遭遇历史", EncounterChecks.CheckEncounterHistoryIsolation),
            new("怪物战失败按损失生命比例奖励金币经验且不扣声望", EncounterChecks.CheckMonsterLoss),
            new("怪物战胜利按怪物等级奖励并抽取待领取卡牌", EncounterChecks.CheckMonsterReward),
            new("怪物技能可作为战利品领取", EncounterChecks.CheckMonsterSkillReward),
            new("棋盘已满时怪物卡牌奖励保持待领取", EncounterChecks.CheckMonsterRewardRetention),
            new("PvP 失败按战斗轮数扣声望", EncounterChecks.CheckPvpReputationLoss),
            new("十次 PvP 胜利结束对局", EncounterChecks.CheckTenPvpWins),
            new("胜利与声望归零同时满足时胜利优先", EncounterChecks.CheckVictoryPriority),
            new("永久摧毁同时移除归属记录和棋盘实例", EncounterChecks.CheckPermanentChangeApplication),
            new("对局快照保留只读结算数据", EncounterChecks.CheckMatchSnapshot),
        ]),
        new("战斗与卡牌能力", [
            new("战斗快照与对局实例隔离", CombatChecks.CheckBattleSnapshotIsolation),
            new("相同输入产生相同战斗日志", CombatChecks.CheckDeterministicBattle),
            new("首次攻击在完整冷却后发动", CombatChecks.CheckFirstActivationTiming),
            new("同 Tick 按玩家方优先进入 FIFO", CombatChecks.CheckStableFifoOrder),
            new("伤害先扣护甲再扣生命", CombatChecks.CheckArmorDamage),
            new("最大生命百分比伤害向下取整并先扣护甲", CombatChecks.CheckMaxHealthPercentDamage),
            new("野猪按己方英雄当前生命比例造成分级伤害", CardCombatChecks.CheckBoarCard),
            new("轻骑兵伤害、成长冷却与人类攻击强化正确结算", CardCombatChecks.CheckLightCavalry),
            new("臂铠按卡牌多重属性额外发动并重复获得护甲", CardCombatChecks.CheckArmguard),
            new("荆棘甲先获得护甲再按英雄当前护甲造成伤害", CardCombatChecks.CheckThornArmor),
            new("魔能盾按己方累计魔法消耗获得护甲", CardCombatChecks.CheckArcaneShield),
            new("军靴使相邻卡牌疾速且对人类翻倍", CardCombatChecks.CheckMilitaryBoots),
            new("神圣狮鹫使相邻己方卡牌疾速并强化获疾速的人类", CardCombatChecks.CheckHolyGriffin),
            new("神圣狮鹫使全部相邻己方卡牌疾速并随机带一张人类飞行", GriffinFlightChecks.TargetsAndReplay),
            new("修女治疗己方英雄并充能另一件光属性卡牌", CardCombatChecks.CheckNun),
            new("大教堂光环为己方光属性卡牌提供可移除的多重", CardCombatChecks.CheckCathedral),
            new("铁匠铺强化装备已有的攻击与护甲能力", CardCombatChecks.CheckBlacksmith),
            new("风之刃分级伤害、自身及多重发动回响按1/2/4/8增长", WindBladeChecks.LevelsAndEchoes),
            new("风之刃多重回放冻结且不带入下场战斗", WindBladeChecks.PlaybackAndReset),
            new("黎明之剑随机摧毁邪恶卡牌并按双方摧毁数倍增攻击", CardCombatChecks.CheckHolySlashingBlade),
            new("教团远征军赋予临时恶魔标签并按存活恶魔增加伤害", CardCombatChecks.CheckOrderCrusader),
            new("无攻击来源时由日蚀结束战斗", CombatChecks.CheckBattleTimeout),
            new("魔法不足时不扣魔法也不发动", CombatChecks.CheckInsufficientMana),
            new("回响产生的事件不会触发其他回响", CombatChecks.CheckEchoDoesNotChain),
            new("灼伤每0.6秒先伤害后减1，中毒每秒穿甲且叠加不衰减", CombatChecks.CheckBurnAndPoison),
            new("永久摧毁只生成永久变化记录", CombatChecks.CheckPermanentDestroy),
        ]),
        new("技能", [
            new("圣骑士人类攻击护甲技能分级、双方筛选、叠加及实际效果正确", HumanAuraSkillChecks.LevelsAndTargets),
            new("无阵营光环技能分级、双方筛选、叠加及实际效果正确", NeutralAuraSkillChecks.LevelsAndTargets),
            new("左右属性光环随边缘目标摧毁重新选择", NeutralAuraSkillChecks.DynamicPositions),
            new("中毒光环覆盖固定属性比例公式且治疗实际结算正确", NeutralAuraSkillChecks.PoisonFormulasAndHealing),
            new("技能使用独立实例并按同级规则合并", SkillChecks.CheckSkillMerge),
            new("空战场的被动技能可触发且回响不会连锁", SkillChecks.CheckSkillBattle),
            new("冲撞仅在己方首张卡牌发动后触发一次并按等级结算", SkillChecks.CheckChargeSkill),
            new("领袖意识仅在己方首张人类发动后充能其他人类且分级正确", LeadershipAwarenessChecks.LevelsAndFirstActivation),
            new("放逐2至4级开战禁锢双方各随机一张，冷却与确定性正确", BanishChecks.LevelsAndTargets),
            new("放逐禁锢叠加、飞行减半、空目标及摧毁过滤正确", BanishChecks.StackingAndFilters),
            new("放逐合并更新数值且旧技能快照保持冻结", BanishChecks.MergeAndSnapshots),
            new("捍卫各等级在战斗开始时累加己方英雄等级护甲", SkillChecks.CheckDefendSkill),
            new("大主教传授捍卫并合并，护甲叠加且战斗输入隔离", SkillChecks.CheckDefendMentorAndIsolation),
            new("至圣斩仅支持4级，大主教按等级筛选并传授", DivineSmiteChecks.DefinitionAndMentor),
            new("至圣斩仅放大己方光属性攻击并按敌方标签判断", DivineSmiteChecks.FilteringAndDamage),
            new("至圣斩响应摧毁、临时标签、转变和召唤变化", DivineSmiteChecks.DynamicConditions),
            new("至圣斩叠加攻击加成和多个来源且配置冻结", DivineSmiteChecks.StackingAndValidation),
        ]),
    ];

    public override void _Ready()
    {
        var progressBar = GetNode<ProgressBar>("Margin/Panel/Margin/Content/Progress");
        progressBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color("302923"), BorderColor = new Color("776345"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        progressBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color("c5ad87"), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });


        _groups.Add(new VerificationGroup("内容词条边界", [
            new("词条身份只读隔离、合法名称及输入边界", ContentDataChecks.DescriptionEntryBoundaries),
        ]));
        _groups.Add(new VerificationGroup("扩展边界与战斗记录", [
            new("飞行/狂暴为布尔状态，重复设置幂等且每场重置", CardStateChecks.BooleanLifecycle),
            new("飞天扫帚分级冷却、进入飞行与1秒疾速正确", FlyingBroomChecks.LevelsAndActivation),
            new("咩咩羊分级疾速、变羊魔棒分级冷却及注册引用正确", PolymorphChecks.LevelsAndHaste),
            new("转变随机筛选、等级保留、完整冷却与旧能力状态清除", PolymorphChecks.TargetsAndReset),
            new("变羊回放身份冻结且下场恢复原卡与位置等级", PolymorphChecks.PlaybackAndRestoration),
            new("小型生命药水分级治疗后仅本场摧毁并在下场恢复", SmallRedPotionChecks.HealingAndConsumption),
            new("小型魔法药水恢复魔法并在恢复后本场摧毁，上限及多重正确", SmallManaPotionChecks.RestoreAndConsume),
            new("狂暴与飞行药水分级、其他目标及本场消耗正确", StatePotionChecks.LevelsAndConsumption),
            new("药水随机可复现，狂暴增伤与飞行减时长正确", StatePotionChecks.RandomAndStateEffects),
            new("炼金釜出售消耗品永久成长、冻结快照和溢出预检正确", AlchemyCauldronChecks.SaleGrowthAndBattle),
            new("弗兰肯特材料出售成长、多重完整发动、升级保留和原画正确", FrankentChecks.SaleGrowthAndBattle),
            new("黑市典当铺分级交易、价值光环、失效撤销、冻结与溢出预检", BlackMarketPawnshopChecks.SaleGrowthAndAura),
            new("军械库三级交易、战场备战拾取成长与失败原子性", EquipmentShopChecks.AcquisitionAndLevels),
            new("军械库及价值光环跨级预览保留价值、成长与卡面攻防", EquipmentShopChecks.LevelPreview),
            new("光辉板甲两级、双方、自身及光属性多重回响成长与筛选", RadiantPlateArmorChecks.LevelsAndEcho),
            new("普通商店本职业池与光属性商店跨职业元素池、刷新购买", OrdinaryAndLightShopChecks.PoolsAndRefresh),
            new("光属性商店圣骑士资格和等级排程、普通商店通用资格", OrdinaryAndLightShopChecks.Scheduling),
            new("全卡各级卡面只显示具体数字、未知上下文为0且保留详情", CardFinalValueChecks.NumericFallbacks),
            new("卢克萨额外发动跳过魔法冷却、包含多重与触发链去重", PaladinActivationChecks.LuksaExtraActivation),
            new("卢克萨摧毁时额外发动与永久摧毁", PaladinActivationChecks.LuksaDestruction),
            new("堕天使分级暗属性计数治疗、随机禁锢与发动飞行", PaladinActivationChecks.FallenAngelHealing),
            new("魔源火铳四级伤害灼伤和魔法边界", PaladinActivationChecks.FirelockLevelsAndMana),
            new("正义天使、小天使发动飞行及星纹天使开战飞行", PaladinActivationChecks.AngelFlightTiming),
            new("光辉剑盾4级、双方200伤害护甲、多重与自身冷却缩减", RadiantSetChecks.SwordShieldCombat),
            new("光辉套装三种卡牌去重计数、光属性装备身份与腰带预览", RadiantSetChecks.SetCountingAndPreview),
            new("光辉标签和冷却光环目标筛选、多来源撤销与永久身份隔离", RadiantSetChecks.AuraFiltersAndLifetime),
            new("光辉腰带三级、双方、装备计数、动态生命与撤销最低1生命", RadiantBeltChecks.LevelsAndLifetime),
            new("护腿四级、双方、4秒装备疾速及相邻多重护甲回响", GreavesChecks.LevelsAndCombat),
            new("随军圣女四级、双方、魔法门槛、6秒周期及人类攻击累加", ArmySaintessChecks.LevelsManaAndTargets),
            new("教团秘法师三级、双方、魔法边界、5秒伤害与己方治疗", OrderArcanistChecks.LevelsManaAndHealing),
            new("十种小型史莱姆分级、双方3秒周期效果、随机目标与原画", ElementSlimeChecks.LevelsAndEffects),
            new("大史莱姆四级伤害、半血边界、原位分裂与确定性", BigSlimeChecks.LevelsAndSplit),
            new("火枪手四级、双方、7秒周期伤害与灼伤及原画", MusketeerChecks.LevelsAndFiring),
            new("弓箭塔四级、双方伤害、敌方多重充能及原画", ArcherTowerChecks.LevelsAndEcho),
            new("建筑与地域分类及双标签", ArcherTowerChecks.BuildingTags),
            new("滚石陷阱两级、双方尺寸摧毁、疾速充能及消耗", RollingBoulderTrapChecks.LevelsAndCombat),
            new("巫师装备四级、魔法支付回响、再生叠加及原画", WizardEquipmentChecks.LevelsAndCombat),
            new("火焰护腕四级、多重伤害与灼伤", FlameGauntletChecks.LevelsAndCombat),
            new("玩具熊三级半血献祭召唤、空位与暴怒熊狂暴倍率", TeddyBearChecks.LevelsAndCombat),
            new("烛台四级灼伤消耗与火属性回响疾速", CandelabrumChecks.LevelsAndCombat),
            new("火焰提灯四级随机迟缓目标与固定灼伤", FlameLanternChecks.LevelsAndTargets),
            new("冰火精灵两级双元素回响、排除自身与充能成长", IcefireSpiritChecks.LevelsAndEchoes),
            new("巫毒娃娃三级、最短完整冷却、并列左侧及中毒", VoodooDollChecks.LevelsAndCombat),
            new("复生仪式双方小型摧毁、同级原位召唤及回响截断", RebirthRitualChecks.SummonsAndFilters),
            new("星纹天使分级、双方、恶魔随机不重复禁锢与无恶魔治疗", StarPatternAngelChecks.LevelsAndBranches),
            new("星纹天使魔法边界、多重、来源失效、条件变化与显示", StarPatternAngelChecks.ManaLifetimeAndDisplay),
            new("军械库标签攻防光环、双方、叠加、失效及冻结", EquipmentShopChecks.AuraFiltersAndLifetime),
            new("碧玉蟾各等级5秒按冻结实际价值施毒且护甲不抵消", JadeToadChecks.LevelsAndPoison),
            new("碧玉蟾只按开战参战实例成长，胜负和战斗类型均适用", JadeToadChecks.GrowthAndParticipation),
            new("碧玉蟾重复成长、升级保留、出售和冻结回放正确", JadeToadChecks.UpgradeSaleAndPlayback),
            new("冒险者日志四级身份、套装与参战价值成长正确", AdventurerJournalChecks.Growth),
            new("冒险者日志最终价值经验、升级与失败出售原子性正确", AdventurerJournalChecks.SaleAndFailures),
            new("冒险者日志套装计数、合并成长和出售撤销正确", AdventurerJournalChecks.SetAndUpgrade),
            new("浆果各等级出售永久增加英雄最大生命，区域与快照正确", BerriesChecks.LevelsAndSales),
            new("浆果出售加成叠加、合并等级更新且失败无残留", BerriesChecks.StackingMergeAndFailures),
            new("树根精粹分级出售永久增加再生、合并叠加与实际战斗恢复正确", RootEssenceChecks.SalesAndRegeneration),
            new("银针草各等级出售强化最左侧治疗卡牌，实际治疗与冻结正确", SilverNeedleGrassChecks.LevelsAndHealing),
            new("银针草出售累加、升级保留、空目标及溢出失败正确", SilverNeedleGrassChecks.StackingAndFailures),
            new("磨刀石各等级出售强化最左侧攻击卡牌，实际伤害与冻结正确", WhetstoneChecks.LevelsAndDamage),
            new("磨刀石出售累加、合并更新、升级保留与溢出失败正确", WhetstoneChecks.StackingAndFailures),
            new("磨刀石目标涵盖全部直接伤害能力与零攻击卡牌", WhetstoneChecks.AttackClassification),
            new("熔岩石四级出售强化最左侧灼伤卡牌、实际灼伤与冻结正确", LavaStoneChecks.LevelsAndSales),
            new("萤火吉丁虫各等级发动与出售最左侧灼伤成长、冻结正确", FireglowJewelBeetleChecks.LevelsAndSales),
            new("萤火吉丁虫支持固定、属性、百分比灼伤及狂暴多重", FireglowJewelBeetleChecks.ClassificationAndPercent),
            new("萤火吉丁虫永久累加、合并升级保留、空目标与溢出拒绝", FireglowJewelBeetleChecks.StackingAndFailures),
            new("毒新妇各等级发动与出售最左侧中毒成长、冻结正确", VenomBrideChecks.LevelsAndSales),
            new("毒新妇支持固定、属性、百分比中毒及狂暴多重", VenomBrideChecks.ClassificationAndPercent),
            new("毒新妇永久累加、合并升级保留、空目标与溢出拒绝", VenomBrideChecks.StackingAndFailures),
            new("全卡各级回响完整触发条件、方向与事件目标文案正确", EchoDescriptionChecks.AllCards),
            new("状态、标签、双侧相邻与事件式光环的回响文案正确", EchoDescriptionChecks.SpecificConditions),
            new("辰光之门身份、3/4级充能及双方回响正确", DawnlightGateChecks.LevelsAndCharge),
            new("辰光之门随机光属性筛选、发动者排除与多重正确", DawnlightGateChecks.FiltersAndRandom),
            new("回响充能立即发动不再连锁且保留冷却、多重与后续正常回响", DawnlightGateChecks.ImmediateChargeAndEchoCutoff),
            new("冒险者猎弓身份、四等级6秒攻击、原画及套装快照正确", AdventurerHuntingBowChecks.IdentityAndDamage),
            new("猎弓怪物胜利同级兽皮，参战筛选、冻结、合并及满盘保留正确", AdventurerHuntingBowChecks.RewardsAndPlacement),
            new("冒险者套装去重、每轮及胜利经验、阈值累积与冻结正确", AdventurerHuntingBowChecks.SetExperience),
            new("冒险者5件生命随等级更新，移区出售撤销且重复刷新不叠加", AdventurerHuntingBowChecks.DynamicHealth),
            new("冒险者背包身份、原画、三级光环和英雄等级乘算正确", AdventurerBackpackChecks.IdentityAndScaling),
            new("背包光环叠加撤销、双方等级隔离、备战辅助筛选与召唤正确", AdventurerBackpackChecks.StackingAndRemoval),
            new("背包与猎弓激活正式2件套，升级更新攻击且旧输入保持冻结", AdventurerBackpackChecks.HeroGrowthAndSet),
            new("冒险者大剑身份、原画、两级英雄等级伤害与8秒冷却正确", AdventurerGreatswordChecks.IdentityAndDamage),
            new("遗迹石像身份、两级伤害护甲与开战禁锢8秒首发正确", RuinStoneStatueChecks.LevelsAndTiming),
            new("碎骨法师身份、分级伤害、摧毁筛选、多重及空目标正确", BonebreakerMageChecks.LevelsAndSacrifice),
            new("碎骨法师外部摧毁回响、尺寸元素归属过滤与狂暴独立", BonebreakerMageChecks.DestructionEcho),
            new("捣蛋鬼蝙蝠身份、分级冷却、固定迟缓与战场筛选正确", MischievousBatChecks.LevelsAndTargets),
            new("捣蛋鬼蝙蝠迟缓累加、多重、飞行减时长及空目标正确", MischievousBatChecks.StackingAndEmptyTargets),
            new("见习剑士身份、四档7秒伤害、原画及快照筛选正确", ApprenticeSwordsmanChecks.LevelsAndArtwork),
            new("见习剑士中型人类回响、多重充能及即时发动筛选正确", ApprenticeSwordsmanChecks.MatchingAndImmediateCharge),
            new("回响尺寸与标签元素筛选组合、去重及非法配置拒绝正确", ApprenticeSwordsmanChecks.SizeFilterComposition),
            new("钢制扳手四档5秒伤害、原画与完整冷却修复正确", SteelWrenchChecks.LevelsAndRestart),
            new("钢制扳手随机筛选、多重逐张修复、永久及备战排除正确", SteelWrenchChecks.SelectionAndMulticast),
            new("修复取消旧队列、保留首次发动历史并恢复冷却光环正确", SteelWrenchChecks.QueueAndRepeatedRepair),
            new("修复避开占位、位置恢复回放及跨战斗隔离正确", SteelWrenchChecks.OccupancyAndPlayback),
            new("英雄升级宝石覆盖轮初、胜利、怪物、出售经验及真实发动", GemGloveChecks.ExperienceChannels),
            new("宝石手套三级攻击、身份及升级文案正确", GemGloveChecks.DefinitionAndAttack),
            new("英雄升级随机宝石覆盖战场备战、连续升级、合并与冻结正确", GemGloveChecks.UpgradeAndSockets),
            new("猛犸近卫两级护甲、敌方发动充能与筛选正确", MammothGuardChecks.ArmorAndCharge),
            new("大天使号两级伤害、20秒冷却、双方分类充能与回响截断正确", ArchangelChecks.LevelsAndCharge),
            new("苍穹塔4级身份、护甲治疗、双方人类充能及光属性独立成长正确", SkyvaultTowerChecks.IdentityAndCombat),
            new("护手四级护甲、多重及双方相邻装备伤害回响正确", GauntletChecks.LevelsAndCombat),
            new("肩甲四级、双方、相邻标签强化与多重", PauldronCaltropsChecks.Pauldron),
            new("铁蒺藜四级、双方敌方疾速回响及陷阱标签", PauldronCaltropsChecks.Caltrops),
            new("尖桩吊闸三级伤害、双方人类及多重充能", PauldronCaltropsChecks.Portcullis),
            new("神像广场两级治疗、独立条件多重和条件失效", StatuePlazaChecks.ConditionsAndHealing),
            new("水井四级治疗、双方灼伤扣减边界、周期和多重", WaterWellChecks.LevelsAndReduction),
            new("守护十字四级、无主动冷却及双方相邻卡牌回响护甲正确", GuardianCrossChecks.LevelsAndCombat),
            new("南瓜灯四级随机迟缓与相邻发动生命再生正确", PumpkinLanternChecks.LevelsAndCombat),
            new("月光古堡暗属性多重光环、叠加与失效正确", MoonlightCastleChecks.LevelsAndCombat),
            new("元素法典三级伤害治疗、魔法消耗及元素去重多重正确", ElementalCodexChecks.LevelsAndCombat),
            new("风魔怪四级多重伤害与自身疾速攻击成长正确", WindFiendChecks.LevelsAndCombat),
            new("四项小魔女元素联动窗口、刷新、消耗与伤害状态正确", ElementSequenceChecks.WindowsAndResults),
            new("亡灵禁术师非暗小型摧毁筛选与原地骷髅召唤正确", UndeadOccultistChecks.SummonsAndFilters),
            new("骷髅巨人三级伤害、16秒冷却与小型摧毁充能正确", SkeletonGiantChecks.DamageAndCharge),
            new("学者之书三级身份、无主动与轮初同级技能学习正确", () => ScholarsBookChecks.LevelsAndCombat() && ScholarsBookChecks.RoundSkills()),
            new("手弩四级伤害、4秒冷却及疾速翻倍提前发动正确", HandCrossbowChecks.LevelsAndHaste),
            new("手弩状态入口、来源加成、多重叠加、衰减与非法配置正确", HandCrossbowChecks.RoutesAndStacking),
            new("魔源发电机分级、无重复充能、魔法支付与双方筛选正确", ManaSourceGeneratorChecks.LevelsAndCharge),
            new("征服者堡垒身份、两级光环、零攻击人类成长与真实伤害正确", ConquerorFortressChecks.LevelsAndGrowth),
            new("冰剑鱼身份、三级伤害、自身冰回响与多重正确", IceSwordfishChecks.LevelsAndDamage),
            new("冰剑鱼随机攻击卡筛选、零攻击成长与确定性隔离正确", IceSwordfishChecks.RandomTargetsAndIsolation),
            new("冰剑鱼冰发动筛选、失效来源与回响不递归正确", IceSwordfishChecks.TriggerBoundaries),
            new("燃爆弹三级7秒伤害、灼伤、消耗顺序、战斗间恢复与原画正确", IncendiaryBombChecks.LevelsAndConsumption),
            new("火焰杯三级灼伤、自身回响、多重与方形原画正确", FlameGobletChecks.LevelsAndBurn),
            new("火焰杯火属性发动筛选、回响不递归与下场重置正确", FlameGobletChecks.TriggersAndReset),
            new("菲尼克斯4级灼伤、首次致命救援、翻倍及下场重置正确", PhoenixChecks.DefinitionAndBattle),
            new("天启四骑士身份、等级及自然日蚀一次性翻倍正确", ApocalypseChecks.LevelsAndNaturalEntry),
            new("提前日蚀、双方同Tick进入、套装生命及日蚀伤害正确", ApocalypseChecks.EarlyEntryAndSetBattle),
            new("日蚀一次性、奇数生命、护甲、备战与摧毁边界正确", ApocalypseChecks.EntryBoundaries),
            new("天启轮开始冷却永久缩减、去重、幂等及升级正确", ApocalypseChecks.RoundSetCooldowns),
            new("菲尼克斯各类致命伤害、敌我、护甲、失效与寂灭边界正确", PhoenixChecks.DamageKindsAndBoundaries),
            new("堡垒叠加、摧毁停触发保留增长与跨战斗隔离正确", ConquerorFortressChecks.StackingAndLifetime),
            new("敌方发动排除失败备战技能回响，强化默认及非法配置正确", ConquerorFortressChecks.TriggerBoundaries),
            new("原卡与召唤物摧毁后可再次召唤，回放占格及跨战斗隔离正确", BonebreakerMageChecks.RepeatedSummoningAndPlayback),
            new("石像双方来源护甲、光环叠加、多重共用冷却与生命周期正确", RuinStoneStatueChecks.TargetsBonusesAndLifecycle),
            new("开锁器各等级出售强化最左侧疾速卡牌，实际时长与冻结正确", LockpickChecks.LevelsAndTiming),
            new("夜盗行衣四级4秒随机疾速、自身目标、敌我筛选与轮初词条正确", NightThiefGarbChecks.LevelsAndHaste),
            new("伪装面具三级跨英雄光环、无阵营及敌我备战筛选正确", DisguiseMaskChecks.LevelsAndAura),
            new("伪装面具光环叠加、撤销、倍率、上限与归属冻结正确", DisguiseMaskChecks.AuraLifetimeAndFrozenIdentity),
            new("伪装面具拾取同级小中型奖励、重复交易与合并等级正确", DisguiseMaskChecks.PickupLevelsAndTransactions),
            new("伪装面具满盘待领、失败无副作用、空池与随机状态正确", DisguiseMaskChecks.PickupFullEmptyAndSharedRandom),
            new("夜盗行衣轮开始幂等、备战排除、当前英雄及同级随机奖励正确", NightThiefGarbChecks.RoundBoundariesAndPool),
            new("夜盗行衣奖励合并、满盘待领、空池及不支持等级筛选正确", NightThiefGarbChecks.MergeFullAndEmpty),
            new("刺客匕首身份原画、四档4秒伤害及首次翻倍正确", AssassinDaggerChecks.LevelsAndTiming),
            new("暗影斗篷身份原画、半血严格阈值与分级隐身到期正确", ShadowCloakChecks.LevelsAndTiming),
            new("暗影斗篷时长累加、备战摧毁排除、治疗后不重复与致命伤害正确", ShadowCloakChecks.StackingAndSourceFilters),
            new("隐身保护生命与护甲、排除持续日蚀伤害且不触发受阻伤害回响", ShadowCloakChecks.DamageKindsAndEchoes),
            new("英雄隐身徽章、暂停倍速跳过与非法配置校验正确", () => ShadowCloakChecks.PlaybackAndValidation(this)),
            new("刺客匕首首次倍率兼容攻击加成、多重、开战狂暴、宝石与冻结预览", AssassinDaggerChecks.MulticastAndSnapshots),
            new("刺客匕首实例双方隔离、魔法不足重试与配置边界正确", AssassinDaggerChecks.IsolationAndValidation),
            new("开锁器出售累加、合并升级、无目标及溢出失败正确", LockpickChecks.StackingAndFailures),
            new("开锁器覆盖相邻倍率、回响、随机及任务疾速，排除纯监听", LockpickChecks.ClassificationAndEcho),
            new("魔法坩埚各等级出售植物成长、区域筛选和7秒施毒正确", MagicCauldronChecks.LevelsAndGrowth),
            new("魔法坩埚20件任务将冷却降至5秒，合并与移区保留进度", MagicCauldronChecks.ThresholdAndMerge),
            new("魔法坩埚失败及重复出售不推进任务，实例与入口隔离", MagicCauldronChecks.FailedSalesAndIsolation),
            new("原木法杖拾取含合并、备战累计、统一发动与升级正确", LogStaffChecks.AcquisitionAndActivation),
            new("魔源锻造仪战斗及永久升级与冷却成长正确", ManaForgeApparatusChecks.LevelsAndUpgrades),
            new("蜘蛛女皇全空位召唤与小型虫族光环正确", SpiderQueenChecks.LevelsAndCombat),
            new("冰刺蠕虫四级迟缓与冰属性出售冷却正确", IceSpikeWormChecks.LevelsAndCombat),
            new("麻痹针蜂等级、随机迟缓目标、叠加及飞行减时长正确", BlackRhinocerosBeetleChecks.LevelsAndSlow),
            new("黑犀金龟分级护甲与针蜂出售冷却正确", BlackRhinocerosBeetleChecks.LevelsAndArmor),
            new("骷髅及骨冢身份、等级、原画与骷髅3秒分级伤害正确", MonaUndeadChecks.LevelsAndDamage),
            new("骨冢3秒同级召唤、骷髅独立冷却、左侧阻挡与确定性正确", MonaUndeadChecks.SummonTimingAndPositions),
            new("破誓者分级随机不重复迟缓、双方光牌动态冷却及倍率正确", OathbreakerChecks.TargetsAndAura),
            new("战锤要塞3/4级伤害、装备计数动态冷却与原画正确", WarhammerFortressChecks.DamageAndCooldown),
            new("辉耀巨龙身份、分级伤害、灼伤加成与累加正确", RadiantDragonChecks.IdentityAndDamage),
            new("辉耀巨龙进入及退出飞行调整冷却进度，状态不跨战斗保留", RadiantDragonChecks.FlyingTransitions),
            new("状态冷却光环叠加倍率、来源失效、零冷却与配置边界正确", RadiantDragonChecks.AuraCompositionAndLimits),
            new("钉头页锤分级伤害、护甲吸收与备战排除正确", FlangedMaceChecks.LevelsAndArmor),
            new("钉头页锤实时护甲倍率、攻击加成、多重与狂暴正确", FlangedMaceChecks.DynamicArmorAndBonuses),
            new("骑佩短剑分级回响伤害、完整占格相邻与多重正确", RidingShortswordChecks.LevelsAndAdjacency),
            new("骑佩短剑按攻击卡牌发动筛选，排除辅助、敌方及备战", RidingShortswordChecks.AttackFilters),
            new("骑佩短剑无主动发动、来源失效及回响截断正确", RidingShortswordChecks.EchoLifetime),
            new("圣殿骑士分级攻击、两侧同级召唤与短剑回响正确", TemplarKnightChecks.LevelsAndEchoes),
            new("相邻召唤边界、阻挡、争用顺序及完整占格正确", TemplarKnightChecks.PositionsAndOrdering),
            new("召唤来源摧毁后独立、永久摧毁无库存变化及注册校验正确", TemplarKnightChecks.LifecycleAndValidation),
            new("召唤物双边回放身份位置冻结、播放控制及战后恢复正确", TemplarKnightChecks.PlaybackAndRestoration),
            new("出售从左查找战场土属性目标，倍率永久累乘且升级保留", () => BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence()),
            new("小数冷却倍率用于首发和重置，兼容增时长、疾速及充能", BlackRhinocerosBeetleChecks.MultiplierBattleTiming),
            new("飞行回响准确定位己方事件卡牌、叠加且不连锁", FlyingBroomChecks.EventTargetsAndStacking),
            new("飞行减半新增迟缓/禁锢，兼容相邻倍率且不影响疾速", CardStateChecks.FlyingDurations),
            new("狂暴覆盖全部伤害公式及中毒/灼伤并在护甲前取整", CardStateChecks.BerserkDamageAndStatus),
            new("狂暴仅加成发动与多重，不重复加成被动或周期伤害", CardStateChecks.BerserkActiveOnly),
            new("怪物与 PvP 使用注入的对手来源", PlaytestVerification.OpponentProviderInjection),
            new("固定战斗的结果、事件和回放状态保留基线", CombatTraceChecks.PreservesTraces),
            new("三种旗帜身份、分级光环与攻击护甲治疗实际效果", StandardAuraChecks.LevelsAndEffects),
            new("旗帜叠加、摧毁贡献扣除、敌方备战排除与冻结治疗显示", StandardAuraChecks.StackingAndRemoval),
            new("圣殿骑士召唤物立即获得战意旗帜光环", StandardAuraChecks.SummonsReceiveAura),
            new("军团旗帜手分级随机人类强化、筛选与战斗累加", StandardAuraChecks.StandardBearerBuff),
            new("军团旗帜手同级随机旗帜召唤、边界占位与确定性", StandardAuraChecks.StandardBearerSummon),
            new("秩序板甲分级护甲、随机单张迟缓、累加与无目标发动", OrderPlateArmorChecks.ArmorAndSlow),
            new("光辉旗帜分级冷却、光属性筛选与冻结回放", RadiantStandardChecks.LevelsAndFiltering),
            new("光辉旗帜百分比叠加、来源失效与永久倍率组合", RadiantStandardChecks.StackingRemovalAndMultiplier),
            new("麦田分级、全体人类充能、即时发动及多重回响", WheatFieldChecks.LevelsAndCharge),
            new("麦田人类疾速回响加速冷却与目标过滤", WheatFieldChecks.EchoAcceleratesCooldown),
            new("传动齿轮两种身份、分级、原画与多重累加正确", TransmissionGearChecks.LevelsAndEffects),
            new("传动齿轮左右方向、空格隔断、来源失效与回响截断", TransmissionGearChecks.DirectionAndLifetime),
            new("传动齿轮疾速加速、充能即时发动与队列去重", TransmissionGearChecks.CooldownAndQueue),
            new("单侧相邻回响与充能拒绝无效配置及非卡牌来源", TransmissionGearChecks.ConfigurationBoundaries),
            new("羽饰头盔身份、原画、2至4级伤害及攻击加成正确", PlumedHelmetChecks.LevelsAndDamage),
            new("羽饰头盔双侧人类发动、多重充能及相邻筛选正确", PlumedHelmetChecks.AdjacentHumanFilters),
            new("羽饰头盔充能即时发动、队列去重与冻结配置正确", PlumedHelmetChecks.ChargeTimingAndBoundaries),
            new("极云四张控制卡身份、等级、原画及冻结能力正确", JiyunControlChecks.IdentityAndLevels),
            new("酒葫芦分级治疗、己方随机迟缓、飞行、多重及衰减", JiyunControlChecks.GourdHealingAndSlow),
            new("斗笠分级护甲、施加方筛选及迟缓充能即时发动", JiyunControlChecks.HatArmorAndCharge),
            new("隐秘竹林木属性筛选、分级迟缓及回响截断", JiyunControlChecks.GroveFilteringAndEchoCutoff),
            new("禅光寺双方攻击分类、事件目标、禁锢叠加及衰减", JiyunControlChecks.TempleTargetsAndDuration),
            new("极云通用迟缓和事件卡牌机制拒绝无效配置", JiyunControlChecks.ConfigurationBoundaries),
            new("飞升者分级、3秒发动及20/60/100次任务即时解锁", AscendantChecks.LevelsAndThresholds),
            new("飞升者100次解锁开战飞行，下一场生效且升级移区保留", AscendantChecks.UnlockedOpeningFlight),
            new("飞升者跨战斗永久成长、升级及备战身份保留", AscendantChecks.CrossBattleAndUpgrade),
            new("飞升者购买及连续合并保留最高等级实例、任务和永久成长", AscendantChecks.MergePreservesProgress),
            new("飞升者自身发动筛选、实例隔离及失败战斗成长", AscendantChecks.FiltersAndLoss),
            new("飞升者任务身份回放、多重阈值及有效攻击百分比灼伤", AscendantChecks.ReplayAndPercentStatus),
            new("飞升者召唤/转变仅本场成长与百分比配置验证", AscendantChecks.TemporarySourcesAndValidation),
        ]));
        _groups.Add(new VerificationGroup("试玩场景集成", [
            new("图鉴套装卡牌补全套装说明且不读取当前对局件数", () => CardCatalogChecks.SetDetails(this)),
            new("分辨率预览确认保存、取消、Escape、超时与隐藏回退正确", () => DisplayLayoutChecks.Settings(this)),
            new("局内菜单图鉴只读、确认放弃整局与旧确认隔离正确", () => UnifiedUiChecks.MenuAndAbandon(this)),
            new("属性词语及数值在两主题保持专属色且不串色", () => UnifiedUiChecks.AttributeText(this)),
            new("上下战场保持比例，备战抽屉只读展开且战斗始终保留敌方", () => DisplayLayoutChecks.MonsterBoard(this)),
            new("发动条读取半Tick冷却，疾速迟缓抵消、禁锢、充能与摧毁清理正确", () => CombatFeedbackChecks.CardLayer(this)),
            new("双方灼伤中毒反馈读取真实伤害事件，暂停与旧回放快照保持冻结", () => CombatFeedbackChecks.RecordedDamage(this)),
            new("技能、遭遇、导师及详情保留等级框，英雄不使用等级色", () => CombatFeedbackChecks.LevelFrames(this)),
            new("单一卡牌详情、直接购买与奖励领取、右键及旧节点门控正确", () => CardInteractionChecks.DirectAcquisition(this)),
            new("大型卡牌悬浮、刷新和缩放后恢复居中位置", () => CardInteractionChecks.HoverRestoration(this)),
            new("详情6px淡入、刷新固定、等级框缩放及关闭重开无累计偏移", () => CardInteractionChecks.DetailsReveal(this)),
            new("正式任务条件奖励、key关联、冻结进度及独立任务区正确", () => CardInteractionChecks.QuestProjection(this)),
            new("技能方形原画贯穿身份、实例、快照及全部支持等级", () => SkillCatalogChecks.ArtworkAndLevels(this)),
            new("技能图鉴搜索、归属、等级、空结果、旧按钮及局外入口正确", () => SkillCatalogChecks.Interaction(this)),
            new("局外图鉴搜索、四维筛选与排序、等级、空结果、旧按钮与返回选角正确", () => CardCatalogChecks.Interaction(this)),
            new("导师技能选项真实渲染、完整提示与旧按钮解绑正确", () => MentorChecks.RenderedChoices(this)),
            new("飞行/狂暴快照、回放投影与真实状态显示一致", () => CardStateChecks.PlaybackAndDisplay(this)),
            new("拖拽出售贯穿真实入口、奖励、失效与战斗限制", () => DragSaleChecks.Transactions(this)),
            new("真实场景事件刷新、敌方可见性与重开事件连接", () => PlaytestVerification.RenderedScene(this)),
            new("三尺寸卡牌贴合棋盘，角部身份与纵向效果布局正确，缩放不改变快照", () => PlaytestVerification.CardAndSlotGeometry(this)),
            new("独立组件渲染无命令、页面互斥与旧按钮解绑", () => PlaytestVerification.IsolatedComponents(this)),
            new("棋盘预览无副作用并在提交时重新验证阻挡", PlaytestVerification.BoardPreviewAndCommit),
            new("拖拽偏移、跨区目标、只读区域与过期对局", () => PlaytestVerification.DragTargets(this)),
            new("拖拽查询、取消选择与旧对局隔离", PlaytestVerification.DragPresenterState),
            new("完整购买失败无残留、满盘合并与重复购买", PlaytestVerification.CompletePurchases),
            new("连续合并释放空间并放置原库存目标", PlaytestVerification.MergePlacementReleasedSpace),
            new("棋盘出售失败保留、现值回补与出售奖励", PlaytestVerification.CompleteSales),
            new("指定技能奖励、过期领取与出售确认隔离", PlaytestVerification.RewardSelectionAndStaleSale),
            new("满盘卡牌奖励保留且可选择后续技能", PlaytestVerification.FullBoardRewardSelection),
            new("真实卡面购买、出售确认、奖励浮层与重开", () => PlaytestVerification.TransactionScene(this)),
            new("真实遭遇原画图卡选择、商店晶体与旧按钮解绑", () => PlaytestVerification.EncounterArtworkScene(this)),
            new("试玩商店按轮次限制等级，首批与付费刷新沿用候选上限", () => PlaytestVerification.TestEncounterLevels(this)),
            new("宝石空孔、镶嵌校验、合并出售、冻结快照与真实卡面正确", () => GemSocketChecks.Lifecycle(this)),
            new("宝石首效果取值、敌方状态、多重升级与冻结正确", GemShopChecks.ActivationAndPrimary),
            new("机关重弩身份、原画、三级伤害与多重共用冷却正确", MechanismHeavyCrossbowChecks.LevelsAndMulticast),
            new("机关重弩相邻机械筛选、双侧不重复与动态冷却正确", MechanismHeavyCrossbowChecks.AdjacencyAndTransitions),
            new("短刃斥候身份、原画、三级疾速伤害与冷却正确", ShortbladeScoutChecks.LevelsAndCooldown),
            new("双截棍身份、三级充能、多重伤害与相邻筛选正确", NunchakuChecks.LevelsAndCombat),
            new("弓骑兵身份、三级伤害及相邻载具筛选正确", MountedArcherChecks.LevelsAndTargets),
            new("弓骑兵疾速累计、到期、冷却加速与确定性正确", MountedArcherChecks.StackingAndCooldown),
            new("信鸽身份、三级疾速与己方战场首尾筛选正确", CarrierPigeonChecks.LevelsAndFilters),
            new("信鸽多重累加、到期、实际加速与首尾失效重选正确", CarrierPigeonChecks.StackingAndTransitions),
            new("短刃斥候外部疾速、多重、增益及来源失效正确", ShortbladeScoutChecks.StatusSourcesAndMulticast),
            new("自身状态获得回响筛选、零时长及连锁截断正确", ShortbladeScoutChecks.FiltersAndChainCutoff),
            new("连枷骑士身份、原画、三级伤害与多重共用冷却正确", FlailKnightChecks.LevelsAndMulticast),
            new("连枷骑士增益、备战摧毁失效与跨战斗隔离正确", FlailKnightChecks.RuntimeIsolation),
            new("精钢战斧身份、原画、四档10秒伤害与快照正确", FineSteelBattleAxeChecks.LevelsAndDamage),
            new("精钢战斧相邻人类、双侧多重及攻击成长正确", FineSteelBattleAxeChecks.AdjacencyAndGrowth),
            new("精钢战斧触发筛选、来源失效及跨战斗隔离正确", FineSteelBattleAxeChecks.FiltersAndIsolation),
            new("伙伴猎犬身份、原画、四档7秒攻击与尺寸筛选快照正确", CompanionHoundChecks.LevelsAndDamage),
            new("伙伴猎犬相邻小型卡牌、双侧多重与攻击累加正确", CompanionHoundChecks.AdjacencyAndGrowth),
            new("伙伴猎犬尺寸位置筛选、来源失效与战斗隔离正确", CompanionHoundChecks.FiltersAndIsolation),
            new("备战卡面展示当前光环、英雄等级与移区后的最终值", CardFinalValueChecks.Preparation),
            new("战斗卡面最终施加量、多重变化与旧回放快照正确", CardFinalValueChecks.Playback),
            new("三种尺寸卡面多重标记、刷新和边界正确", () => CardFinalValueChecks.FaceScene(this)),
            new("HTML同步全卡当前值描述与统一资源、冷却配色", UiPrototypeSyncChecks.Definitions),
            new("等级宝石预览保留成长与实例且不修改原快照", UiPrototypeSyncChecks.Preview),
            new("放大预览等级、宝石、还原、描述方括号及Escape正确", () => UiPrototypeSyncChecks.Scene(this)),
            new("战场、全卡等级预览与一秒冷却下限显示正确", () => UiPrototypeSyncChecks.CooldownDetails(this)),
            new("宝石商店两砧板、扣费刷新、一次选择与过期隔离正确", GemShopChecks.FeesAndRefresh),
            new("宝石商店真实砧板拖放、预览只读及离店关闭正确", () => GemShopChecks.DragScene(this)),
            new("英雄初始属性、插画身份、选角缩略图与头像正确", () => HeroArtworkChecks.Check(this)),
            new("圣骑士动效时钟独立、刷新保留、隐藏暂停与英雄切换正确", () => HeroArtworkChecks.CheckMotion(this)),
            new("圣骑士选角配音支持鼠标键盘、重复重播、刷新保留与隐藏停播", () => HeroArtworkChecks.CheckVoice(this)),
        ]));
        var batches = _groups.SelectMany((group, index) => group.Checks.Chunk(20).Select((checks, part) =>
            new VerificationBatch(new StringName($"group-{index + 1:00}-part-{part + 1:00}"),
                $"{group.Name} · 第{part + 1}批", checks))).ToArray();
        var arguments = OS.GetCmdlineUserArgs();
        if (arguments.Contains("--verify-list"))
        {
            GD.Print("VERIFY_BATCHES_JSON=" + System.Text.Json.JsonSerializer.Serialize(batches.Select(batch =>
                new { key = batch.Key.ToString(), name = batch.Name, count = batch.Checks.Count })));
            GetTree().Quit();
            return;
        }
        var requestedBatch = arguments.FirstOrDefault(value => value.StartsWith("--verify-batch=", StringComparison.Ordinal));
        var requestedGroup = arguments.FirstOrDefault(value => value.StartsWith("--verify-group=", StringComparison.Ordinal));
        if (requestedGroup is not null)
        {
            var prefix = requestedGroup["--verify-group=".Length..] + "-part-";
            var selected = batches.Where(batch => batch.Key.ToString().StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Callable.From((Action)(async () =>
            {
                if (selected.Length == 0) { GD.Print($"未知验证分类：{requestedGroup}"); GetTree().Quit(2); return; }
                GetTree().Quit(await RunBatches(selected) ? 0 : 1);
            })).CallDeferred();
            return;
        }
        if (requestedBatch is not null)
        {
            var key = new StringName(requestedBatch["--verify-batch=".Length..]);
            var selected = batches.FirstOrDefault(batch => batch.Key == key);
            Callable.From((Action)(async () =>
            {
                if (selected is null) { GD.Print($"未知验证批次：{key}"); GetTree().Quit(2); return; }
                GetTree().Quit(await RunChecks(selected.Name, selected.Checks) ? 0 : 1);
            })).CallDeferred();
            return;
        }
        var categoryButtons = GetNode<HFlowContainer>("Margin/Panel/Margin/Content/Categories");
        var allChecks = _groups.SelectMany(group => group.Checks).ToArray();
        var allButton = new Button
        {
            Text = $"分批验证 ({allChecks.Length})",
            CustomMinimumSize = new Vector2(150, 42),
        };
        categoryButtons.AddChild(allButton);
        allButton.Pressed += async () => await RunBatches(batches);

        foreach (var group in _groups)
        {
            var button = new Button
            {
                Text = $"{group.Name} ({group.Checks.Count})",
                CustomMinimumSize = new Vector2(170, 42),
            };
            categoryButtons.AddChild(button);
            button.Pressed += async () => await RunBatches(batches.Where(batch => batch.Checks.Any(group.Checks.Contains)).ToArray());
        }

        GetNode<Label>("Margin/Panel/Margin/Content/Title").Text = "选择验证范围";
        GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text =
            $"当前共 {allChecks.Length} 项，选择上方按钮运行全部验证或指定分类。";
        GetNode<Button>("Margin/Panel/Margin/Content/Back").Pressed +=
            () => GetTree().ChangeSceneToFile("res://Playtest.tscn");
        if (OS.GetCmdlineUserArgs().Contains("--verify-progress"))
        {
            Callable.From((Action)(async () =>
            {
                try
                {
                    var valid = progressBar.Value == 0;
                    if (OS.GetCmdlineUserArgs().Contains("--progress-fullhd"))
                    {
                        GetWindow().Size = new Vector2I(1920, 1080);
                        GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
                    }
                    var captureDirectory = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "output/verification-progress");
                    System.IO.Directory.CreateDirectory(captureDirectory);
                    async Task<bool> Observe(int completed, int total, string file)
                    {
                        valid &= _progressCompleted == completed && _progressTotal == total && progressBar.Value == completed;
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        RenderingServer.ForceDraw();
                        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(captureDirectory, file));
                        return true;
                    }
                    var success = await RunBatches(new VerificationBatch[]
                    {
                        new(new StringName("progress-1"), "进度检查 第一批", new VerificationCheck[]
                        {
                            new("起点", () => Observe(0, 4, "native-start.png")),
                            new("预期失败", () => false)
                        }),
                        new(new StringName("progress-2"), "进度检查 第二批", new VerificationCheck[]
                        {
                            new("跨批累计", () => Observe(2, 4, "native-half.png")),
                            new("预期异常", (Func<bool>)(() => throw new InvalidOperationException("进度检查")))
                        })
                    });
                    valid &= !success && _progressCompleted == 4 && progressBar.Value == 4;
                    var summary = GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text;
                    valid &= summary.Contains("✗ 预期失败") && summary.Contains("✗ 预期异常")
                        && summary.Contains("InvalidOperationException: 进度检查");
                    await Observe(4, 4, "native-complete.png");
                    valid &= await RunChecks("重新运行", new VerificationCheck[] { new("重新计数", () => Observe(0, 1, "native-reset.png")) });
                    valid &= progressBar.Value == 1 && _progressTotal == 1;
                    GD.Print($"验证进度条：{(valid ? "通过" : "失败")}");
                    GetTree().Quit(valid ? 0 : 1);
                }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--verify-display-settings"))
            Callable.From((Action)(() =>
            {
                try { var passed = DisplayLayoutChecks.Settings(this); GD.Print($"显示设置专项：{passed}"); GetTree().Quit(passed ? 0 : 1); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-card-catalog"))
            Callable.From((Action)(async () =>
            {
                try { var passed = await CardCatalogChecks.Interaction(this); GD.Print($"卡牌图鉴专项：{passed}"); GetTree().Quit(passed ? 0 : 1); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-skill-catalog"))
            Callable.From((Action)(() =>
            {
                try { var passed = SkillCatalogChecks.Interaction(this); GD.Print($"技能图鉴专项：{passed}"); GetTree().Quit(passed ? 0 : 1); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-mana-forge"))
            Callable.From((Action)(() => { try { var passed = ManaForgeApparatusChecks.LevelsAndUpgrades(); GD.Print($"魔源锻造仪专项：{passed}"); GetTree().Quit(passed ? 0 : 1); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mana-forge"))
            Callable.From((Action)(async () => { try { await ManaForgeApparatusChecks.Capture(this); GetTree().Quit(); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-spider-queen"))
            Callable.From((Action)(() => { try { var passed = SpiderQueenChecks.LevelsAndCombat(); GD.Print($"蜘蛛女皇专项：{passed}"); GetTree().Quit(passed ? 0 : 1); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-spider-queen"))
            Callable.From((Action)(async () => { try { await SpiderQueenChecks.Capture(this); GetTree().Quit(); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-ice-spike-worm"))
            Callable.From((Action)(() => { var passed = IceSpikeWormChecks.LevelsAndCombat(); GD.Print($"冰刺蠕虫专项：{passed}"); GetTree().Quit(passed ? 0 : 1); })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-quest-whole-scroll"))
            Callable.From((Action)(async () => { try { await IceSpikeWormChecks.Capture(this); GetTree().Quit(); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-hover-scroll"))
            Callable.From((Action)(async () => { try { await CardInteractionChecks.HoverScrolling(this); GetTree().Quit(); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-armor-shattering-maul"))
        {
            Callable.From((Action)(async () =>
            {
                try { await ArmorShatteringMaulChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        }
        if (OS.GetCmdlineUserArgs().Contains("--capture-training-camp"))
        {
            Callable.From((Action)(async () =>
            {
                try { await TrainingCampChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        }
        if (OS.GetCmdlineUserArgs().Contains("--capture-black-market-pawnshop"))
        {
            Callable.From((Action)(async () =>
            {
                try { await BlackMarketPawnshopChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        }
        if (OS.GetCmdlineUserArgs().Contains("--capture-equipment-shop"))
            Callable.From((Action)(async () =>
            {
                try { await EquipmentShopChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-radiant-plate-armor"))
            Callable.From((Action)(async () =>
            {
                try { await RadiantPlateArmorChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-radiant-belt"))
            Callable.From((Action)(async () =>
            {
                try { await RadiantBeltChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-greaves"))
            Callable.From((Action)(async () =>
            {
                try { await GreavesChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-army-saintess"))
            Callable.From((Action)(async () =>
            {
                try { await ArmySaintessChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-order-arcanist"))
            Callable.From((Action)(async () =>
            {
                try { await OrderArcanistChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-myriad-atlas"))
            Callable.From((Action)(() =>
            {
                var passed = MyriadAtlasChecks.GrowthAndQuest();
                GD.Print($"万象舆图专项验证：{passed}"); GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-myriad-atlas"))
            Callable.From((Action)(async () =>
            {
                try { await MyriadAtlasChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-rebirth-ritual"))
            Callable.From((Action)(async () =>
            {
                try { await RebirthRitualChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-human-aura-skills"))
            Callable.From((Action)(() =>
            {
                var passed = HumanAuraSkillChecks.LevelsAndTargets();
                GD.Print($"训练与御敌分级和实际结算={passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-needle-wasp"))
            Callable.From((Action)(async () =>
            {
                try { await BlackRhinocerosBeetleChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-element-sequences"))
            Callable.From((Action)(async () =>
            {
                try { await ElementSequenceChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-skeleton-giant"))
            Callable.From((Action)(() =>
            {
                var passed = SkeletonGiantChecks.DamageAndCharge() && CombatTraceChecks.PreservesTraces();
                GD.Print($"骷髅巨人及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-skeleton-giant"))
            Callable.From((Action)(async () =>
            {
                try { await SkeletonGiantChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-undead-occultist"))
            Callable.From((Action)(() =>
            {
                var summon = UndeadOccultistChecks.SummonsAndFilters();
                var trace = CombatTraceChecks.PreservesTraces();
                GD.Print($"亡灵禁术师={summon} 固定回归={trace}");
                GetTree().Quit(summon && trace ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-undead-occultist"))
            Callable.From((Action)(async () =>
            {
                try { await UndeadOccultistChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-element-sequences"))
            Callable.From((Action)(() =>
            {
                var windows = ElementSequenceChecks.WindowsAndResults();
                var trace = CombatTraceChecks.PreservesTraces();
                GD.Print($"元素联动={windows} 固定回归={trace}");
                GetTree().Quit(windows && trace ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-wind-fiend"))
            Callable.From((Action)(() =>
            {
                var passed = WindFiendChecks.LevelsAndCombat() && FlyingBroomChecks.LevelsAndActivation()
                    && FlyingBroomChecks.EventTargetsAndStacking() && CombatTraceChecks.PreservesTraces();
                GD.Print($"风魔怪、风属性飞天扫帚及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-wind-fiend"))
            Callable.From((Action)(async () =>
            {
                try { await WindFiendChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-gu-insect-pouch"))
            Callable.From((Action)(() =>
            {
                try { var passed = GuInsectPouchChecks.Combat() && CombatTraceChecks.PreservesTraces(); GD.Print($"虫蛊袋与固定回归：{passed}"); GetTree().Quit(passed ? 0 : 1); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-gu-insect-pouch"))
            Callable.From((Action)(async () =>
            {
                try { await GuInsectPouchChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-catalog-sets") || OS.GetCmdlineUserArgs().Contains("--capture-catalog-sets"))
            Callable.From((Action)(async () =>
            {
                try { var passed = await CardCatalogChecks.SetDetails(this); await CardInteractionChecks.HoverScrolling(this); GD.Print($"图鉴套装说明及悬停关闭：{passed}"); GetTree().Quit(passed ? 0 : 1); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-mantis-scorpion"))
            Callable.From((Action)(() =>
            {
                try
                {
                    var passed = MantisScorpionChecks.LevelsAndCombat() && MantisScorpionChecks.Sales() && CombatTraceChecks.PreservesTraces();
                    GD.Print($"风刃螳螂、静水蝎与固定战斗回归：{passed}"); GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mantis-scorpion"))
            Callable.From((Action)(async () =>
            {
                try { await MantisScorpionChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-magister"))
            Callable.From((Action)(() =>
            {
                try
                {
                    var passed = MagisterChecks.Combat() && MagisterChecks.Rewards() && CombatTraceChecks.PreservesTraces()
                        && ScholarsBookChecks.RoundSkills();
                    GD.Print($"魔导士套装与固定回归：{passed}"); GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-magister"))
            Callable.From((Action)(async () =>
            {
                try { await MagisterChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-scholars-book"))
            Callable.From((Action)(() =>
            {
                var combat = ScholarsBookChecks.LevelsAndCombat();
                var round = ScholarsBookChecks.RoundSkills();
                var trace = CombatTraceChecks.PreservesTraces();
                GD.Print($"学者之书身份={combat} 轮初技能={round} 固定回归={trace}");
                GetTree().Quit(combat && round && trace ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-scholars-book"))
            Callable.From((Action)(async () =>
            {
                try { await ScholarsBookChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-elemental-codex"))
            Callable.From((Action)(() =>
            {
                var passed = ElementalCodexChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"元素法典及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-elemental-codex"))
            Callable.From((Action)(async () =>
            {
                try { await ElementalCodexChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-moonlight-castle"))
            Callable.From((Action)(() =>
            {
                var passed = MoonlightCastleChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"月光古堡及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-moonlight-castle"))
            Callable.From((Action)(async () =>
            {
                try { await MoonlightCastleChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-pumpkin-lantern"))
            Callable.From((Action)(async () =>
            {
                try { await PumpkinLanternChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-pumpkin-lantern"))
            Callable.From((Action)(() =>
            {
                var passed = PumpkinLanternChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"南瓜灯及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-needle-wasp"))
            Callable.From((Action)(() =>
            {
                var slow = BlackRhinocerosBeetleChecks.LevelsAndSlow();
                var armor = BlackRhinocerosBeetleChecks.LevelsAndArmor();
                var sale = BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence();
                var timing = BlackRhinocerosBeetleChecks.MultiplierBattleTiming();
                var trace = CombatTraceChecks.PreservesTraces();
                GD.Print($"迟缓={slow} 护甲={armor} 出售={sale} 冷却={timing} 回归={trace}");
                var passed = slow && armor && sale && timing && trace;
                GD.Print($"麻痹针蜂与黑犀金龟专项验证：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-voodoo-doll"))
            Callable.From((Action)(() =>
            {
                var passed = VoodooDollChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"巫毒娃娃及固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-voodoo-doll"))
            Callable.From((Action)(async () =>
            {
                try { await VoodooDollChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-icefire-spirit"))
            Callable.From((Action)(() =>
            {
                var passed = IcefireSpiritChecks.LevelsAndEchoes();
                var regression = CombatTraceChecks.PreservesTraces() && CandelabrumChecks.LevelsAndCombat()
                    && FlameLanternChecks.LevelsAndTargets() && FlameGobletChecks.LevelsAndBurn() && FlameGobletChecks.TriggersAndReset();
                GD.Print($"冰火精灵专项：{passed}，固定战斗及元素回响回归：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-icefire-spirit"))
            Callable.From((Action)(async () =>
            {
                try { await IcefireSpiritChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-spirit-tag"))
            Callable.From((Action)(() =>
            {
                var passed = PhoenixChecks.DefinitionAndBattle() && StarPatternAngelChecks.LevelsAndBranches()
                    && StarPatternAngelChecks.ManaLifetimeAndDisplay() && AscendantChecks.LevelsAndThresholds()
                    && CardDescriptionChecks.AllLevels();
                GD.Print($"精灵标签及关联内容验证：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-flame-lantern"))
            Callable.From((Action)(() =>
            {
                var passed = FlameLanternChecks.LevelsAndTargets();
                var regression = CombatTraceChecks.PreservesTraces() && MischievousBatChecks.LevelsAndTargets();
                GD.Print($"火焰提灯专项：{passed}，固定战斗及迟缓回归：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-flame-lantern"))
            Callable.From((Action)(async () =>
            {
                try { await FlameLanternChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-bonebreaker-mage"))
            Callable.From((Action)(() =>
            {
                var levels = BonebreakerMageChecks.LevelsAndSacrifice();
                var echo = BonebreakerMageChecks.DestructionEcho();
                var summon = BonebreakerMageChecks.RepeatedSummoningAndPlayback();
                var regression = CombatTraceChecks.PreservesTraces() && RebirthRitualChecks.SummonsAndFilters() && CandelabrumChecks.LevelsAndCombat();
                GD.Print($"碎骨法师：分级={levels}，摧毁回响={echo}，召唤回放={summon}，回归={regression}");
                GetTree().Quit(levels && echo && summon && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-candelabrum"))
            Callable.From((Action)(() =>
            {
                var passed = CandelabrumChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"烛台专项与固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-candelabrum"))
            Callable.From((Action)(async () =>
            {
                try { await CandelabrumChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-teddy-bear"))
            Callable.From((Action)(() =>
            {
                var passed = TeddyBearChecks.LevelsAndCombat();
                var regression = CombatTraceChecks.PreservesTraces() && RebirthRitualChecks.SummonsAndFilters();
                GD.Print($"玩具熊与暴怒熊专项：{passed}，固定战斗与召唤回归：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-teddy-bear"))
            Callable.From((Action)(async () =>
            {
                try { await TeddyBearChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-flame-gauntlet"))
            Callable.From((Action)(() =>
            {
                var passed = FlameGauntletChecks.LevelsAndCombat() && CombatTraceChecks.PreservesTraces();
                GD.Print($"火焰护腕专项与固定战斗回归：{passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-flame-gauntlet"))
            Callable.From((Action)(async () =>
            {
                try { await FlameGauntletChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-wizard-equipment"))
            Callable.From((Action)(() =>
            {
                var passed = WizardEquipmentChecks.LevelsAndCombat();
                var regression = CombatTraceChecks.PreservesTraces();
                GD.Print($"巫师装备专项验证：{passed}，固定战斗与回放基线：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-wizard-equipment"))
            Callable.From((Action)(async () =>
            {
                try { await WizardEquipmentChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-rebirth-ritual"))
            Callable.From((Action)(() =>
            {
                var passed = RebirthRitualChecks.SummonsAndFilters();
                var regression = CombatTraceChecks.PreservesTraces() && RollingBoulderTrapChecks.LevelsAndCombat();
                GD.Print($"复生仪式专项：{passed}，固定战斗与滚石陷阱回归：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-rolling-boulder-trap"))
            Callable.From((Action)(() =>
            {
                var passed = RollingBoulderTrapChecks.LevelsAndCombat();
                var regression = CombatTraceChecks.PreservesTraces();
                GD.Print($"滚石陷阱专项验证：{passed}，固定战斗与回放基线：{regression}");
                GetTree().Quit(passed && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-rolling-boulder-trap"))
            Callable.From((Action)(async () =>
            {
                try { await RollingBoulderTrapChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-archer-tower"))
            Callable.From((Action)(async () =>
            {
                try { await ArcherTowerChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-element-slimes"))
            Callable.From((Action)(async () =>
            {
                try { await ElementSlimeChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-pauldron-caltrops"))
            Callable.From((Action)(async () =>
            {
                try { await PauldronCaltropsChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-pauldron-caltrops"))
            Callable.From((Action)(() =>
            {
                var shoulder = PauldronCaltropsChecks.Pauldron();
                var trap = PauldronCaltropsChecks.Caltrops();
                var portcullis = PauldronCaltropsChecks.Portcullis();
                var armory = EquipmentShopChecks.AcquisitionAndLevels() && EquipmentShopChecks.LevelPreview()
                    && EquipmentShopChecks.AuraFiltersAndLifetime();
                var regression = CombatTraceChecks.PreservesTraces() && CardCombatChecks.CheckBlacksmith()
                    && IceLotusChecks.LevelsAndCombat() && CardDescriptionChecks.AllLevels();
                GD.Print($"肩甲={shoulder}，铁蒺藜={trap}，尖桩吊闸={portcullis}，军械库={armory}，相关回归={regression}");
                GetTree().Quit(shoulder && trap && portcullis && armory && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-statue-plaza"))
            Callable.From((Action)(async () =>
            {
                try { await StatuePlazaChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-statue-plaza"))
            Callable.From((Action)(() =>
            {
                var card = StatuePlazaChecks.ConditionsAndHealing();
                var regression = CombatTraceChecks.PreservesTraces() && DivineSenseAndAngelChecks.Angel() && CardDescriptionChecks.AllLevels();
                GD.Print($"神像广场={card}，条件多重与固定战斗回归={regression}");
                GetTree().Quit(card && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-water-well"))
            Callable.From((Action)(async () =>
            {
                try { await WaterWellChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-water-well"))
            Callable.From((Action)(() =>
            {
                var card = WaterWellChecks.LevelsAndReduction();
                var regression = CombatTraceChecks.PreservesTraces() && MusketeerChecks.LevelsAndFiring() && CardDescriptionChecks.AllLevels();
                GD.Print($"水井={card}，灼伤与固定战斗回归={regression}");
                GetTree().Quit(card && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-fire-slime-monster"))
            Callable.From((Action)(() =>
            {
                var monster = FireSlimeMonsterChecks.DefinitionAndBattle() && FireSlimeMonsterChecks.Rewards();
                var baa = BaaMonsterChecks.DefinitionAndBattle() && BaaMonsterChecks.Rewards()
                    && WindmillDaisyChecks.LevelsHealingAndSales();
                var regression = CombatTraceChecks.PreservesTraces();
                GD.Print($"火史莱姆怪物={monster}，咩咩80生命与治愈回归={baa}，固定战斗={regression}");
                GetTree().Quit(monster && baa && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-lava-stone"))
            Callable.From((Action)(() =>
            {
                var card = LavaStoneChecks.LevelsAndSales();
                var regression = FireglowJewelBeetleChecks.ClassificationAndPercent()
                    && FireglowJewelBeetleChecks.StackingAndFailures() && CombatTraceChecks.PreservesTraces()
                    && CardDescriptionChecks.AllLevels();
                GD.Print($"熔岩石={card}，灼伤成长与固定战斗回归={regression}");
                GetTree().Quit(card && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-lava-stone"))
            Callable.From((Action)(async () =>
            {
                try { await LavaStoneChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-jailbreaker"))
            Callable.From((Action)(() =>
            {
                var definition = JailbreakerChecks.DefinitionAndBattle();
                var rewards = JailbreakerChecks.Rewards();
                GD.Print($"逃遁的破誓者：身份、原画mipmap与战斗={definition}，奖励={rewards}");
                GetTree().Quit(definition && rewards ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-element-slimes"))
            Callable.From((Action)(() =>
            {
                var small = ElementSlimeChecks.LevelsAndEffects();
                var big = BigSlimeChecks.LevelsAndSplit();
                var regression = CombatTraceChecks.PreservesTraces()
                    && PolymorphChecks.LevelsAndHaste() && PolymorphChecks.TargetsAndReset()
                    && PolymorphChecks.PlaybackAndRestoration() && TemplarKnightChecks.PlaybackAndRestoration()
                    && BonebreakerMageChecks.RepeatedSummoningAndPlayback() && DawnLongbowChecks.CancellationAndFrozenCount()
                    && CardDescriptionChecks.AllLevels();
                GD.Print($"史莱姆专项：小型={small}，大史莱姆={big}，固定战斗与转变召唤回归={regression}");
                GetTree().Quit(small && big && regression ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-musketeer"))
            Callable.From((Action)(async () =>
            {
                try { await MusketeerChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-star-pattern-angel"))
            Callable.From((Action)(async () =>
            {
                try { await StarPatternAngelChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-shadow-cloak"))
            Callable.From((Action)(async () =>
            {
                try { await ShadowCloakChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-night-thief-garb"))
            Callable.From((Action)(async () =>
            {
                try { await NightThiefGarbChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-disguise-mask"))
            Callable.From((Action)(async () =>
            {
                try { await DisguiseMaskChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-card-interactions"))
            Callable.From((Action)(async () =>
            {
                try { await CardInteractionChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-portrait-motion"))
            Callable.From((Action)(async () =>
            {
                try { await HeroArtworkChecks.CaptureMotion(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-catalog"))
            Callable.From((Action)(async () =>
            {
                try { await CardCatalogChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-card-catalog"))
            Callable.From((Action)(async () =>
            {
                var passed = CardCatalogChecks.Definitions() && await CardCatalogChecks.Interaction(this);
                GD.Print($"卡牌图鉴定义、筛选与只读交互：{passed}"); GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mechanism-heavy-crossbow"))
            Callable.From((Action)(async () =>
            {
                try { await MechanismHeavyCrossbowChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-shortblade-scout"))
            Callable.From((Action)(async () =>
            {
                try { await ShortbladeScoutChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-training-dummy"))
            Callable.From((Action)(async () =>
            {
                try { await TrainingDummyChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-lion-king-shield"))
            Callable.From((Action)(async () =>
            {
                try { await LionKingShieldChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-heavy-cavalry"))
            Callable.From((Action)(async () =>
            {
                try { await HeavyCavalryChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-siege-ballista"))
            Callable.From((Action)(async () =>
            {
                try { await SiegeBallistaChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-ice-lotus"))
            Callable.From((Action)(async () =>
            {
                try { await IceLotusChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-windmill-daisy"))
            Callable.From((Action)(async () =>
            {
                try { await WindmillDaisyChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mounted-archer"))
            Callable.From((Action)(async () =>
            {
                try { await MountedArcherChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-carrier-pigeon"))
            Callable.From((Action)(async () =>
            {
                try { await CarrierPigeonChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-companion-hound"))
            Callable.From((Action)(async () =>
            {
                try { await CompanionHoundChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-state-potions"))
            Callable.From((Action)(async () =>
            {
                try { await StatePotionChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-flail-knight"))
            Callable.From((Action)(async () =>
            {
                try { await FlailKnightChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-fine-steel-battle-axe"))
            Callable.From((Action)(async () =>
            {
                try { await FineSteelBattleAxeChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-card-final-values"))
            Callable.From((Action)(async () =>
            {
                try { await CardFinalValueChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-skill-catalog"))
            Callable.From((Action)(async () =>
            {
                try { await SkillCatalogChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-forest"))
            Callable.From((Action)(async () =>
            {
                try { await DuskSongJungleChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mentors"))
            Callable.From((Action)(async () =>
            {
                try { await MentorChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-gem-shop"))
            Callable.From((Action)(async () =>
            {
                try { await GemShopChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-gem-ratio"))
            Callable.From((Action)(() =>
            {
                var passed = GemShopChecks.ActivationAndPrimary() && CombatTraceChecks.PreservesTraces();
                GD.Print($"宝石属性换算与固定战斗回归={passed}");
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify"))
            Callable.From((Action)(async () =>
            {
                var passed = await RunBatches(batches);
                GetTree().Quit(passed ? 0 : 1);
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-ui-sync"))
            Callable.From((Action)(async () =>
            {
                try { await UiPrototypeSyncChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-incendiary-bomb"))
            Callable.From((Action)(async () =>
            {
                try { await IncendiaryBombChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-ice-swordfish"))
            Callable.From((Action)(async () =>
            {
                try { await IceSwordfishChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-gem-glove"))
            Callable.From((Action)(async () =>
            {
                try { await GemGloveChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-card-artwork-redraw"))
            Callable.From((Action)(async () =>
            {
                try { await CardArtworkRedrawCapture.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-card-description-copy"))
            Callable.From((Action)(async () =>
            {
                try { await CardDescriptionChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-judgment-hammer"))
            Callable.From((Action)(async () =>
            {
                try { await CardArtworkRedrawCapture.Capture(this, judgmentHammer: true); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-paladin-redraw"))
            Callable.From((Action)(async () =>
            {
                try { await CardArtworkRedrawCapture.Capture(this, true); GetTree().Quit(); }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-warship-gunner"))
            Callable.From((Action)(async () =>
            {
                try { await WarshipGunnerChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-warship-helmsman"))
            Callable.From((Action)(async () =>
            {
                try { await WarshipHelmsmanChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-hand-crossbow"))
            Callable.From((Action)(async () =>
            {
                try { await HandCrossbowChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-guardian-cross"))
            Callable.From((Action)(async () =>
            {
                try { await GuardianCrossChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-gauntlet"))
            Callable.From((Action)(async () =>
            {
                try { await GauntletChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-skyvault-tower"))
            Callable.From((Action)(async () =>
            {
                try { await SkyvaultTowerChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-archangel"))
            Callable.From((Action)(async () =>
            {
                try { await ArchangelChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mammoth-guard"))
            Callable.From((Action)(async () =>
            {
                try { await MammothGuardChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-mana-source-generator"))
            Callable.From((Action)(async () =>
            {
                try { await ManaSourceGeneratorChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-moonshadow-cat"))
            Callable.From((Action)(async () =>
            {
                try { await MoonshadowCatChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-adventurer-journal"))
            Callable.From((Action)(async () =>
            {
                try { await AdventurerJournalChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-flame-goblet"))
            Callable.From((Action)(async () =>
            {
                try { await FlameGobletChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-phoenix"))
            Callable.From((Action)(async () =>
            {
                try { await PhoenixChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-warhammer-fortress"))
            Callable.From((Action)(async () =>
            {
                try { await WarhammerFortressChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-dawn-longbow"))
            Callable.From((Action)(async () =>
            {
                try { await DawnLongbowChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-farmer"))
            Callable.From((Action)(async () =>
            {
                try { await FarmerChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-venom-bride"))
            Callable.From((Action)(async () =>
            {
                try { await VenomBrideChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-dawnlight-gate"))
            Callable.From((Action)(async () =>
            {
                try { await DawnlightGateChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--capture-apocalypse"))
            Callable.From((Action)(async () =>
            {
                try { await ApocalypseChecks.Capture(this); GetTree().Quit(); }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-native-drag"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    var passed = await PlaytestVerification.NativeDrag(this) && await DragSaleChecks.Native(this) && await GemShopChecks.NativeDrag(this);
                    GD.Print($"原生拖拽输入验证：{(passed ? "通过" : "失败")}");
                    GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-gem-shop-drag"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    var passed = await GemShopChecks.NativeDrag(this);
                    GD.Print($"原生拖拽输入验证：{(passed ? "通过" : "失败")}");
                    GetTree().Quit(passed ? 0 : 1);
                }
                catch (Exception error) { GD.Print(error); GetTree().Quit(1); }
            })).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--verify-unified-input"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    var passed = await UnifiedUiChecks.NativeMenu(this);
                    GD.Print($"统一菜单原生键盘验证：{(passed ? "通过" : "失败")}"); GetTree().Quit(passed ? 0 : 1);
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

    private async Task<bool> RunBatches(IReadOnlyList<VerificationBatch> batches)
    {
        if (_runningBatches || _running) return false;
        _runningBatches = true;
        _progressCompleted = 0;
        _progressTotal = batches.Sum(batch => batch.Checks.Count);
        var results = new List<string>(); var passed = 0; var completedBatches = 0;
        try
        {
            foreach (var batch in batches)
            {
                _progressBatch = $"第 {++completedBatches} / {batches.Count} 批";
                var failures = new List<string>();
                var success = await RunChecks(batch.Name, batch.Checks, failures);
                if (success) passed++;
                results.Add($"{(success ? "✓" : "✗")} {batch.Name} ({batch.Checks.Count}项)");
                results.AddRange(failures.Select(failure => "    " + failure.Replace("\n", "\n    ")));
            }
            var title = GetNode<Label>("Margin/Panel/Margin/Content/Title");
            title.Text = $"分批验证：{passed}/{batches.Count}批通过";
            title.Modulate = passed == batches.Count ? new Color("75d69c") : new Color("ff7b72");
            _progressBatch = "验证完成";
            UpdateProgress();
            GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text = string.Join("\n", results);
            GD.Print(title.Text + "\n" + string.Join("\n", results));
            return passed == batches.Count;
        }
        finally { _runningBatches = false; }
    }

    private async Task<bool> RunChecks(
        string groupName,
        IReadOnlyList<VerificationCheck> checks, List<string>? failures = null)
    {
        if (_running) return false;
        _running = true;
        if (!_runningBatches)
        {
            _progressCompleted = 0;
            _progressTotal = checks.Count;
            _progressBatch = "单批验证";
        }
        GetNode<Label>("Margin/Panel/Margin/Content/Title").Modulate = Colors.White;
        UpdateProgress();
        var results = new List<string>(checks.Count);
        var passedCount = 0;
        foreach (var check in checks)
        {
            GetNode<Label>("Margin/Panel/Margin/Content/Title").Text = $"{groupName} · {results.Count + 1}/{checks.Count} · {check.Name}";
            // 先呈现当前进度，再执行可能耗时的同步检查。
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"VERIFY_CHECK_BEGIN {check.Name}");
            try
            {
                var passed = await check.Run();
                if (passed) passedCount++;
                results.Add($"{(passed ? "✓" : "✗")} {check.Name}");
                if (!passed) failures?.Add(results[^1]);
            }
            catch (Exception exception)
            {
                results.Add($"✗ {check.Name}\n    {exception.GetType().Name}: {exception.Message}");
                failures?.Add(results[^1]);
            }
            _progressCompleted++;
            UpdateProgress();
            GetNode<Label>("Margin/Panel/Margin/Content/Results/Output").Text = string.Join("\n", results);
            // 让延迟回调及QueueFree在下一项之前完成，避免同帧积压。
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var allPassed = passedCount == checks.Count;
        var title = GetNode<Label>("Margin/Panel/Margin/Content/Title");
        var output = GetNode<Label>("Margin/Panel/Margin/Content/Results/Output");
        title.Text = $"{groupName}：{(allPassed ? "通过" : "失败")} ({passedCount}/{checks.Count})";
        if (!_runningBatches)
        {
            _progressBatch = "验证完成";
            UpdateProgress();
        }
        title.Modulate = allPassed ? new Color("75d69c") : new Color("ff7b72");
        output.Text = string.Join("\n", results);
        GD.Print($"{title.Text}\n{output.Text}");
        _running = false;
        return allPassed;
    }

}
