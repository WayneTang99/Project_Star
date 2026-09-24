using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Content.Monsters;
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Presentation;

/// <summary>Provides the in-Godot manual verification entry for implementation step one.</summary>
public sealed partial class PhaseOneVerification : Control
{
    private readonly List<(string Name, Func<bool> Check)> _checks =
    [
        ("0.2 秒等于 2 个战斗 Tick", CheckTickConversion),
        ("非固定步长时间会被拒绝", CheckInvalidTickConversion),
        ("相同 Seed 产生相同随机序列", CheckDeterministicRandom),
        ("保存随机状态后可以继续序列", CheckRandomResume),
        ("成功 Result 正确携带数据", CheckSuccessfulResult),
        ("失败 Result 使用 StringName 错误码", CheckFailureCode),
        ("身份字段创建后保持只读", CheckReadonlyIdentity),
        ("General 是合法的普通属性", CheckGeneralElement),
        ("双属性按规范顺序保存", CheckElementNormalization),
        ("非法属性组合会被拒绝", CheckInvalidElements),
        ("多来源 Modifier 自然累加", CheckModifierStacking),
        ("移除 Modifier 不影响其他来源", CheckModifierRemoval),
        ("尺寸自动生成型号标签和占格数", CheckSizeTag),
        ("未知标签显示原始 Key", CheckUnknownTagDisplay),
        ("卡牌定义组合身份、属性与标签", CheckCardDefinition),
        ("注册表登记英雄、卡牌与遭遇定义", CheckDefinitionDiscovery),
        ("注册表拒绝同类型重复 Key", CheckDuplicateDefinition),
        ("套装注册表拒绝未知套装归属", CheckUnknownCardSet),
        ("定义中的初始属性不可修改", CheckFrozenDefinition),
        ("工厂创建完全独立的卡牌实例", CheckIndependentInstances),
        ("新对局可以选择英雄并生成卡牌", CheckMatchCreation),
        ("不同对局之间不共享状态", CheckSessionIsolation),
        ("每轮开始按英雄收入幂等发放金钱", CheckRoundIncome),
        ("尺寸与等级按翻倍表计算初始价值", CheckInitialValues),
        ("特殊卡牌可以覆写价值系数", CheckSpecialValueCoefficient),
        ("获得卡牌统一设置半价现值", CheckAcquisitionSources),
        ("半价出现小数时向下取整", CheckAcquiredValueRounding),
        ("等级变化不会重算当前价值", CheckLevelDoesNotRecalculateValue),
        ("同名同级卡牌连续合并且保留目标实例", CheckCardMergeUpgrade),
        ("卡牌可以限制等级并应用分级配置", CheckCardLevelDefinitions),
        ("无阵营无属性卡牌支持分级价值且没有能力", CheckBeastHideDefinition),
        ("钻石固定为4级且获得后价值增加20", CheckDiamondDefinition),
        ("珠宝袋出售后自动获得同级材料卡", CheckJewelryBagSaleReward),
        ("百宝箱出售后获得三件同级小型材料", CheckTreasureChestSaleReward),
        ("型号商店只提供当前英雄归属的对应尺寸卡牌", CheckSizeShopCardPools),
        ("野猪使用默认属性并携带三张1级卡牌和1级冲撞", CheckBoarMonsterDefinition),
        ("体能训练按英雄等级结算可扩展选项", CheckPhysicalTraining),
        ("垃圾填埋场生成固定与加权选项并结算奖励", CheckLandfill),
        ("购买按报价扣款并登记卡牌归属", CheckPurchase),
        ("余额不足时交易无任何修改", CheckInsufficientWealth),
        ("出售按现值回补且只能一次", CheckSale),
        ("卡牌可以直接放入空闲格", CheckDirectPlacement),
        ("左右方向都能形成推挤方案", CheckPushDirections),
        ("距离和影响数相同时优先右推", CheckRightTieBreak),
        ("不可推挤卡牌会阻止放置", CheckUnpushableCard),
        ("同盘移动会先释放自身原位", CheckSameZoneMove),
        ("跨区失败时两个区域均不改变", CheckCrossZoneRollback),
        ("同一实例只存在于一个棋盘区域", CheckUniqueBoardLocation),
        ("卡牌不能越过棋盘容量边界", CheckBoardCapacity),
        ("套装按战场不同卡牌累计阈值并精确移除", CheckCardSetBonuses),
        ("显式套装目标可覆盖战场卡牌与英雄但不覆盖备战区", CheckCardSetExplicitTargets),
        ("套装战斗来源在成员摧毁后仍按快照触发", CheckCardSetBattleSource),
        ("任务按卡牌实例累计且仅在战场区激活解锁能力", CheckCardQuestProgress),
        ("任务解锁的战斗能力随来源卡牌摧毁而失效", CheckCardQuestBattleDestroy),
        ("移出棋盘后卡牌可以出售", CheckRemoveThenSell),
        ("战斗快照与对局实例隔离", CheckBattleSnapshotIsolation),
        ("相同输入产生相同战斗日志", CheckDeterministicBattle),
        ("首次攻击在完整冷却后发动", CheckFirstActivationTiming),
        ("同 Tick 按玩家方优先进入 FIFO", CheckStableFifoOrder),
        ("伤害先扣护甲再扣生命", CheckArmorDamage),
        ("最大生命百分比伤害向下取整并先扣护甲", CheckMaxHealthPercentDamage),
        ("野猪按己方英雄当前生命比例造成分级伤害", CheckBoarCard),
        ("轻骑兵伤害、成长冷却与人类攻击强化正确结算", CheckLightCavalry),
        ("臂铠按卡牌多重属性额外发动并重复获得护甲", CheckArmguard),
        ("魔能盾按己方累计魔法消耗获得护甲", CheckArcaneShield),
        ("军靴使相邻卡牌疾速且对人类翻倍", CheckMilitaryBoots),
        ("神圣狮鹫使相邻人类疾速并在其获得疾速时强化攻击", CheckHolyGriffin),
        ("大教堂光环为己方光属性卡牌提供可移除的多重", CheckCathedral),
        ("铁匠铺强化装备已有的攻击与护甲能力", CheckBlacksmith),
        ("黎明之剑随机摧毁邪恶卡牌并按双方摧毁数倍增攻击", CheckHolySlashingBlade),
        ("教团远征军赋予临时恶魔标签并按存活恶魔增加伤害", CheckOrderCrusader),
        ("无攻击来源时由日蚀结束战斗", CheckBattleTimeout),
        ("魔法不足时不扣魔法也不发动", CheckInsufficientMana),
        ("回响产生的事件不会触发其他回响", CheckEchoDoesNotChain),
        ("灼伤扣护甲且中毒无视护甲", CheckBurnAndPoison),
        ("永久摧毁只生成永久变化记录", CheckPermanentDestroy),
        ("普通回合三选一且包含商店", CheckNormalEncounterChoices),
        ("相同 Seed 生成相同遭遇候选", CheckDeterministicEncounters),
        ("候选生成时立即记录为已出现", CheckEncounterSeenHistory),
        ("第 4 回合固定三个怪物遭遇", CheckMonsterTurn),
        ("第 8 回合固定 PvP 并推进轮次", CheckPvpTurnAdvance),
        ("新对局不会继承遭遇历史", CheckEncounterHistoryIsolation),
        ("怪物战失败按损失生命比例奖励金币经验且不扣声望", CheckMonsterLoss),
        ("怪物战胜利按怪物等级奖励并抽取待领取卡牌", CheckMonsterReward),
        ("怪物技能可作为战利品领取", CheckMonsterSkillReward),
        ("棋盘已满时怪物卡牌奖励保持待领取", CheckMonsterRewardRetention),
        ("PvP 失败按战斗轮数扣声望", CheckPvpReputationLoss),
        ("十次 PvP 胜利结束对局", CheckTenPvpWins),
        ("胜利与声望归零同时满足时胜利优先", CheckVictoryPriority),
        ("永久摧毁同时移除归属记录和棋盘实例", CheckPermanentChangeApplication),
        ("对局快照保留只读结算数据", CheckMatchSnapshot),
        ("技能使用独立实例并按同级规则合并", CheckSkillMerge),
        ("空战场的被动技能可触发且回响不会连锁", CheckSkillBattle),
        ("冲撞仅在己方首张卡牌发动后触发一次并按等级结算", CheckChargeSkill),
    ];

    public override void _Ready()
    {
        var results = new List<string>();
        var allPassed = true;

        foreach (var (name, check) in _checks)
        {
            try
            {
                var passed = check();
                allPassed &= passed;
                results.Add($"{(passed ? "✓" : "✗")} {name}");
            }
            catch (Exception exception)
            {
                allPassed = false;
                results.Add($"✗ {name}\n    {exception.GetType().Name}: {exception.Message}");
            }
        }

        var title = GetNode<Label>("Margin/Panel/Margin/Content/Title");
        var output = GetNode<Label>("Margin/Panel/Margin/Content/Results/Output");
        title.Text = allPassed ? "第 1～9 步验证通过" : "第 1～9 步验证失败";
        title.Modulate = allPassed ? new Color("75d69c") : new Color("ff7b72");
        output.Text = string.Join("\n", results);
        GD.Print($"{title.Text}\n{output.Text}");
        GetNode<Button>("Margin/Panel/Margin/Content/Back").Pressed +=
            () => GetTree().ChangeSceneToFile("res://Playtest.tscn");
    }

    private static bool CheckTickConversion()
    {
        var tick = BattleTick.FromPeriodicSeconds(0.2m);
        return tick.Value == 2 && tick.ToSeconds() == 0.2m;
    }

    private static bool CheckInvalidTickConversion()
    {
        try
        {
            _ = BattleTick.FromPeriodicSeconds(0.15m);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool CheckDeterministicRandom()
    {
        var first = new SeededRandom(42);
        var second = new SeededRandom(42);

        for (var index = 0; index < 20; index++)
        {
            if (first.NextInt(-10, 25) != second.NextInt(-10, 25))
            {
                return false;
            }
        }

        return first.State == second.State;
    }

    private static bool CheckRandomResume()
    {
        var original = new SeededRandom(7);
        _ = original.NextInt(0, 100);
        var resumed = new SeededRandom(original.State);
        return original.NextInt(0, 100) == resumed.NextInt(0, 100);
    }

    private static bool CheckSuccessfulResult()
    {
        var result = Result<int>.Success(12);
        return result.IsSuccess && result.Value == 12 && result.Failure is null;
    }

    private static bool CheckFailureCode()
    {
        var expectedCode = new StringName("card.not_found");
        var result = Result.Fail(new Failure(expectedCode, "Card was not found."));
        return result.IsFailure && result.Failure?.Code == expectedCode;
    }

    private static bool CheckReadonlyIdentity()
    {
        return typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.Key))?.CanWrite == false
            && typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.ElementKeys))?.CanWrite == false;
    }

    private static bool CheckGeneralElement()
    {
        var identity = CreateCardIdentity([GameElements.General]);
        return identity.ElementKeys.Count == 1 && identity.ElementKeys[0] == GameElements.General;
    }

    private static bool CheckElementNormalization()
    {
        var identity = CreateCardIdentity([GameElements.Dark, GameElements.Fire]);
        return identity.ElementKeys.Count == 2
            && identity.ElementKeys[0] == GameElements.Fire
            && identity.ElementKeys[1] == GameElements.Dark;
    }

    private static bool CheckInvalidElements()
    {
        return RejectsElements([])
            && RejectsElements([GameElements.Fire, GameElements.Fire])
            && RejectsElements([GameElements.Fire, GameElements.Water, GameElements.Wind])
            && RejectsElements([new StringName("Unknown")]);
    }

    private static bool CheckModifierStacking()
    {
        var attack = new StringName("Attack");
        var attributes = new ModifiableAttributeSet(new Dictionary<StringName, int> { [attack] = 10 });
        attributes.ApplyModifier(new StatModifier(ModifierId.New(), EntityId.New(), attack, 4));
        attributes.ApplyModifier(new StatModifier(ModifierId.New(), EntityId.New(), attack, 6));
        return attributes.GetBaseValue(attack) == 10 && attributes.GetFinalValue(attack) == 20;
    }

    private static bool CheckModifierRemoval()
    {
        var armor = new StringName("Armor");
        var attributes = new ModifiableAttributeSet();
        var first = new StatModifier(ModifierId.New(), EntityId.New(), armor, 5);
        var second = new StatModifier(ModifierId.New(), EntityId.New(), armor, 7);
        attributes.ApplyModifier(first);
        attributes.ApplyModifier(second);
        var removed = attributes.RemoveModifier(first.Id);
        return removed && attributes.GetFinalValue(armor) == 7;
    }

    private static bool CheckSizeTag()
    {
        var identity = CreateCardIdentity([GameElements.Ice], CardSize.Large);
        var tags = TagSet.ForCard(identity.Size, [GameTags.Equipment]);
        return identity.OccupiedSlots == 3 && tags.Contains(GameTags.Large) && tags.Contains(GameTags.Equipment);
    }

    private static bool CheckUnknownTagDisplay()
    {
        var unknown = new StringName("Dragon");
        return TagDisplayNames.Get(unknown) == "Dragon";
    }

    private static bool CheckCardDefinition()
    {
        var definition = new VerificationCardDefinition();
        return definition.Attributes.Identity.DisplayName == "验证之剑"
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Tags.Contains(GameTags.Small)
            && definition.Tags.Contains(GameTags.Equipment);
    }

    private static bool CheckDefinitionDiscovery()
    {
        var registry = CreateVerificationRegistry();
        return registry.Heroes.Count == 1
            && registry.Cards.Count == 1
            && registry.Skills.Count == 1
            && registry.Encounters.Count == 7
            && registry.Cards.ContainsKey(new StringName("verification.sword"));
    }

    private static bool CheckDuplicateDefinition()
    {
        try
        {
            _ = DefinitionRegistry.Create([new VerificationCardDefinition(), new VerificationCardDefinition()]);
            return false;
        }
        catch (DefinitionValidationException exception)
        {
            return exception.Message.Contains("Duplicate card key", StringComparison.Ordinal);
        }
    }

    private static bool CheckFrozenDefinition()
    {
        var definition = new VerificationCardDefinition();
        try
        {
            definition.Attributes.Persistent.SetBaseValue(new StringName("Value"), 10);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool CheckIndependentInstances()
    {
        var factory = new EntityFactory();
        var definition = new VerificationCardDefinition();
        var first = factory.CreateCard(definition);
        var second = factory.CreateCard(definition);
        var value = new StringName("Value");
        first.Attributes.Persistent.SetBaseValue(value, 25);

        return first.Id != second.Id
            && first.Attributes.Persistent.GetBaseValue(value) == 25
            && second.Attributes.Persistent.GetBaseValue(value) == 0;
    }

    private static bool CheckMatchCreation()
    {
        var factory = new EntityFactory();
        var session = new MatchSession(42);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        session.Player.Inventory.Add(factory.CreateCard(new VerificationCardDefinition()));

        return session.Player.Hero is not null
            && session.Player.Inventory.Cards.Count == 1
            && session.Board.Battlefield.Count == 0
            && session.Board.Bench.Count == 0
            && session.Random.Seed == 42;
    }

    private static bool CheckSessionIsolation()
    {
        var factory = new EntityFactory();
        var first = new MatchSession(1);
        var second = new MatchSession(1);
        first.Player.Inventory.Add(factory.CreateCard(new VerificationCardDefinition()));

        return first.Id != second.Id
            && first.Player.Inventory.Cards.Count == 1
            && second.Player.Inventory.Cards.Count == 0
            && !ReferenceEquals(first.Board, second.Board)
            && !ReferenceEquals(first.EncounterSchedule, second.EncounterSchedule);
    }

    private static bool CheckRoundIncome()
    {
        var session = new CreateMatchService(new EntityFactory()).Create(
            1,
            10,
            new VerificationHeroDefinition());
        var service = new RoundIncomeService();
        var repeated = service.SettleCurrentRound(session);
        session.Progress.Round = 2;
        var nextRound = service.SettleCurrentRound(session);
        var snapshot = MatchSnapshot.From(session);
        return session.Player.Income == 5
            && repeated.Value == 0
            && nextRound.Value == 5
            && session.Player.Wealth == 20
            && session.Progress.IncomeSettledThroughRound == 2
            && snapshot.Income == 5
            && session.Events.Count(value => value is IncomeGrantedEvent) == 2;
    }

    private static bool CheckInitialValues()
    {
        var small = new EconomyCardDefinition(CardSize.Small);
        var medium = new EconomyCardDefinition(CardSize.Medium);
        var large = new EconomyCardDefinition(CardSize.Large);
        var levelFiveSmall = new FiveLevelEconomyCardDefinition(CardSize.Small);
        var levelFiveMedium = new FiveLevelEconomyCardDefinition(CardSize.Medium);
        var levelFiveLarge = new FiveLevelEconomyCardDefinition(CardSize.Large);
        return CardValueCalculator.CalculateInitialValue(small, 1) == 2
            && CardValueCalculator.CalculateInitialValue(small, 2) == 4
            && CardValueCalculator.CalculateInitialValue(small, 3) == 8
            && CardValueCalculator.CalculateInitialValue(small, 4) == 16
            && CardValueCalculator.CalculateInitialValue(levelFiveSmall, 5) == 16
            && CardValueCalculator.CalculateInitialValue(medium, 1) == 4
            && CardValueCalculator.CalculateInitialValue(medium, 2) == 8
            && CardValueCalculator.CalculateInitialValue(medium, 3) == 16
            && CardValueCalculator.CalculateInitialValue(medium, 4) == 32
            && CardValueCalculator.CalculateInitialValue(levelFiveMedium, 5) == 32
            && CardValueCalculator.CalculateInitialValue(large, 1) == 6
            && CardValueCalculator.CalculateInitialValue(large, 2) == 12
            && CardValueCalculator.CalculateInitialValue(large, 3) == 24
            && CardValueCalculator.CalculateInitialValue(large, 4) == 48
            && CardValueCalculator.CalculateInitialValue(levelFiveLarge, 5) == 48;
    }

    private static bool CheckSizeShopCardPools()
    {
        var session = new MatchSession(1);
        session.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        CardDefinition[] cards =
        [
            new BeastHideCardDefinition(),
            new LightCavalryCardDefinition(),
            new ArcaneShieldCardDefinition(),
            new MilitaryBootsCardDefinition(),
            new JudgmentHammerCardDefinition(),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin")),
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("other")),
        ];
        var service = new ShopCardPoolService();
        var smallShop = new SmallShopEncounterDefinition();
        var mediumShop = new MediumShopEncounterDefinition();
        var largeShop = new LargeShopEncounterDefinition();
        var small = service.GetEligibleCards(session, smallShop, cards);
        var medium = service.GetEligibleCards(session, mediumShop, cards);
        var large = service.GetEligibleCards(session, largeShop, cards);

        var refreshSession = new MatchSession(2, 10);
        refreshSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        CardDefinition[] refreshCards =
        [
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "one"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "two"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "three"),
            new ShopVerificationCardDefinition(CardSize.Small, new StringName("paladin"), "four"),
        ];
        var stock = service.CreateStock(refreshSession, smallShop, refreshCards);
        var initialUnique = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).Distinct().Count() == 3;
        var refreshed = service.Refresh(refreshSession, stock);
        var refreshedUnique = stock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key).Distinct().Count() == 3;
        var repeatedRefresh = service.Refresh(refreshSession, stock);

        var limitedSession = new MatchSession(3, 10);
        limitedSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        var limitedStock = service.CreateStock(limitedSession, mediumShop,
        [
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("paladin"), "one"),
            new ShopVerificationCardDefinition(CardSize.Medium, new StringName("paladin"), "two"),
        ]);
        var limitedRefresh = service.Refresh(limitedSession, limitedStock);

        var poorSession = new MatchSession(4, 1);
        poorSession.Player.SelectHero(new EntityFactory().CreateHero(new PaladinHeroDefinition()));
        var poorStock = service.CreateStock(poorSession, smallShop, refreshCards);
        var poorKeys = string.Join("|", poorStock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key));
        var poorRefresh = service.Refresh(poorSession, poorStock);

        return smallShop.Kind == EncounterKind.Shop
            && smallShop.CardSize == CardSize.Small
            && smallShop.Level == 1
            && mediumShop.CardSize == CardSize.Medium
            && mediumShop.Level == 1
            && largeShop.CardSize == CardSize.Large
            && largeShop.Level == 2
            && small.Count == 2
            && small.All(card => card.Attributes.Identity.FactionKey == new StringName("paladin")
                && card.Attributes.Identity.Size == CardSize.Small)
            && small.Any(card => card.Attributes.Identity.Key == new StringName("card.military_boots"))
            && medium.Count == 2
            && medium.Any(card => card.Attributes.Identity.Key == new StringName("card.light_cavalry"))
            && medium.Any(card => card.Attributes.Identity.Key == new StringName("card.arcane_shield"))
            && large.Count == 1
            && large[0].Attributes.Identity.Key == new StringName("card.judgment_hammer")
            && stock.Offers.Count == 3
            && initialUnique
            && refreshed.IsSuccess
            && refreshedUnique
            && stock.HasRefreshed
            && !stock.CanRefresh
            && refreshSession.Player.Wealth == 8
            && repeatedRefresh.IsFailure
            && limitedStock.Offers.Count == 2
            && !limitedStock.CanRefresh
            && limitedRefresh.IsFailure
            && limitedSession.Player.Wealth == 10
            && poorRefresh.IsFailure
            && poorSession.Player.Wealth == 1
            && !poorStock.HasRefreshed
            && poorKeys == string.Join("|", poorStock.Offers.Select(offer => offer.Definition.Attributes.Identity.Key))
            && ShopCardPoolService.GetRefreshCost(1) == 2
            && ShopCardPoolService.GetRefreshCost(2) == 4
            && ShopCardPoolService.GetRefreshCost(3) == 6
            && ShopCardPoolService.GetRefreshCost(4) == 8;
    }

    private static bool CheckBoarMonsterDefinition()
    {
        var definition = new BoarMonsterDefinition();
        var registry = DefinitionRegistry.Create([definition, new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var session = new MatchSession(1);
        session.Progress.Turn = 4;
        var choices = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true).Generate(session).Value!;
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, definition);
        return registry.Monsters.ContainsKey(new StringName("monster.boar"))
            && definition.Attributes.Identity.DisplayName == "野猪"
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) == 100
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Armor) == 0
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxMana) == 100
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Mana) == 0
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.ManaRegen) == 10
            && definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.HealthRegen) == 0
            && definition.Level == 1
            && definition.Skills.Count == 1
            && definition.Skills[0].SkillKey == new StringName("skill.charge")
            && definition.Skills[0].Level == 1
            && definition.Cards.Count == 3
            && definition.Cards[0].CardKey == new StringName("card.beast_hide")
            && definition.Cards[0].Level == 1
            && definition.Cards[0].BoardStart == 0
            && definition.Cards[1].CardKey == new StringName("card.beast_hide")
            && definition.Cards[1].Level == 1
            && definition.Cards[1].BoardStart == 1
            && definition.Cards[2].CardKey == new StringName("card.boar")
            && definition.Cards[2].Level == 1
            && definition.Cards[2].BoardStart == 2
            && choices.Count == 1
            && choices[0].Key == new StringName("monster.boar")
            && choices[0].Kind == EncounterKind.Monster
            && opponent.Player.Hero!.Attributes.Identity.DisplayName == "野猪"
            && opponent.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards.Count == 3
            && opponent.Player.Skills.Items.Count == 1
            && opponent.Player.Skills.Items[0].Attributes.Identity.Key == new StringName("skill.charge")
            && opponent.Player.Skills.Items[0].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Board.Battlefield.Count == 3
            && opponent.Player.Inventory.Cards[0].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards[1].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Player.Inventory.Cards[2].Attributes.Identity.Key == new StringName("card.boar")
            && opponent.Player.Inventory.Cards[2].Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && opponent.Board.Battlefield.Placements[2].Start == 2
            && opponent.Board.Battlefield.Placements[2].Size == 2;
    }

    private static bool CheckPhysicalTraining()
    {
        var definition = new PhysicalTrainingEncounterDefinition();
        var registry = DefinitionRegistry.Create([new PaladinHeroDefinition(), definition]);
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var resolver = new ResolveEncounterOptionService(factory, CreateBoardService(), Array.Empty<CardDefinition>());
        var heroDefinition = new PaladinHeroDefinition();

        var healthSession = matches.Create(1, 0, heroDefinition);
        healthSession.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 3);
        var healthOptions = resolver.CreateOptionSet(healthSession, definition);
        var healthResult = resolver.Resolve(healthSession, healthOptions, definition.Options[0].Key);
        var healthOpponent = matches.Create(2, 0, heroDefinition);
        var healthSetup = new BattleSetupFactory().Create(healthSession, healthOpponent, 1, new BattleTick(1));

        var regenSession = matches.Create(3, 0, heroDefinition);
        regenSession.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 3);
        var regenOptions = resolver.CreateOptionSet(regenSession, definition);
        var regenResult = resolver.Resolve(regenSession, regenOptions, definition.Options[1].Key);
        var regenOpponent = matches.Create(4, 0, heroDefinition);
        var regenSetup = new BattleSetupFactory().Create(regenSession, regenOpponent, 1, new BattleTick(1));
        var invalidResult = resolver.Resolve(
            regenSession,
            regenOptions,
            new StringName("encounter.physical_training.unknown"));

        return registry.Encounters.ContainsKey(new StringName("encounter.physical_training"))
            && definition.Kind == EncounterKind.Other
            && definition.MinimumRound == 1
            && definition.MaximumRound == 99
            && definition.Options.Count == 2
            && definition.Options[0].Key == new StringName("encounter.physical_training.max_health")
            && definition.Options[1].Key == new StringName("encounter.physical_training.health_regen")
            && healthSession.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && healthResult.IsSuccess
            && healthResult.Value!.Changes.Count == 1
            && healthResult.Value.Changes[0].Amount == 30
            && healthSetup.Player.Hero.MaxHealth == 130
            && regenResult.IsSuccess
            && regenResult.Value!.Changes.Count == 1
            && regenResult.Value.Changes[0].Amount == 3
            && regenSetup.Player.Hero.HealthRegen == 3
            && invalidResult.IsFailure
            && regenSession.Player.Hero.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.HealthRegen) == 3;
    }

    private static bool CheckLandfill()
    {
        var definition = new LandfillEncounterDefinition();
        var heroDefinition = new PaladinHeroDefinition();
        CardDefinition[] cards = [new ArmguardCardDefinition(), new MilitaryBootsCardDefinition(), new BeastHideCardDefinition()];
        var registryItems = new List<object> { heroDefinition, definition };
        registryItems.AddRange(cards.Cast<object>());
        var registry = DefinitionRegistry.Create(registryItems);
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var board = CreateBoardService();
        var resolver = new ResolveEncounterOptionService(factory, board, cards);
        var wealthKey = new StringName("encounter.landfill.wealth");
        var factionKey = new StringName("encounter.landfill.faction_small_card");
        var materialKey = new StringName("encounter.landfill.material_small_card");

        var deterministicA = resolver.CreateOptionSet(matches.Create(77, 0, heroDefinition), definition);
        var deterministicB = resolver.CreateOptionSet(matches.Create(77, 0, heroDefinition), definition);
        if (deterministicA.Options.Count != 2
            || deterministicA.Options[0].Key != wealthKey
            || deterministicA.Options[1].Key != deterministicB.Options[1].Key)
        {
            return false;
        }

        MatchSession? factionSession = null;
        EncounterOptionSet? factionOptions = null;
        MatchSession? materialSession = null;
        EncounterOptionSet? materialOptions = null;
        for (ulong seed = 1; seed <= 1000 && (factionSession is null || materialSession is null); seed++)
        {
            var session = matches.Create(seed, 0, heroDefinition);
            var options = resolver.CreateOptionSet(session, definition);
            if (options.Options[1].Key == factionKey && factionSession is null)
                (factionSession, factionOptions) = (session, options);
            if (options.Options[1].Key == materialKey && materialSession is null)
                (materialSession, materialOptions) = (session, options);
        }
        if (factionSession is null || factionOptions is null || materialSession is null || materialOptions is null)
            return false;

        var wealthSession = matches.Create(88, 0, heroDefinition);
        var wealthOptions = resolver.CreateOptionSet(wealthSession, definition);
        var wealthResult = resolver.Resolve(wealthSession, wealthOptions, wealthKey);
        var repeatedResult = resolver.Resolve(wealthSession, wealthOptions, wealthKey);
        var factionResult = resolver.Resolve(factionSession, factionOptions, factionKey);
        var materialResult = resolver.Resolve(materialSession, materialOptions, materialKey);

        var fullSession = matches.Create(99, 0, heroDefinition);
        var economy = new CardEconomyService(factory);
        for (var index = 0; index < 20; index++)
        {
            var blocker = economy.AcquireCard(fullSession, cards[2], 4, CardAcquisitionSource.Reward).Value!;
            var zone = index < 10 ? BoardZone.Battlefield : BoardZone.Bench;
            _ = board.PlaceCard(fullSession, blocker.Id, zone, index % 10);
        }
        EncounterOptionSet? fullCardOptions = null;
        for (var attempts = 0; attempts < 1000; attempts++)
        {
            var options = resolver.CreateOptionSet(fullSession, definition);
            if (options.Options[1].Key == factionKey)
            {
                fullCardOptions = options;
                break;
            }
        }
        if (fullCardOptions is null) return false;
        var inventoryBefore = fullSession.Player.Inventory.Cards.Count;
        var fullResult = resolver.Resolve(fullSession, fullCardOptions, factionKey);

        return registry.Encounters.ContainsKey(new StringName("encounter.landfill"))
            && definition.Level == 1
            && definition.Options.Count == 3
            && definition.OptionSlots.Count == 2
            && definition.OptionSlots[1].Candidates[0].Weight == 60
            && definition.OptionSlots[1].Candidates[1].Weight == 40
            && wealthResult.IsSuccess
            && wealthResult.Value!.WealthGained == 2
            && wealthSession.Player.Wealth == 7
            && repeatedResult.IsFailure
            && factionResult.Value?.GrantedCard?.Attributes.Identity.FactionKey == new StringName("paladin")
            && factionResult.Value.GrantedCard.Attributes.Identity.Size == CardSize.Small
            && materialResult.Value?.GrantedCard?.Tags.Contains(GameTags.Material) == true
            && materialResult.Value.GrantedCard.Attributes.Identity.Size == CardSize.Small
            && fullResult.IsSuccess
            && fullResult.Value!.CardRewardSkipped
            && fullResult.Value.GrantedCard is null
            && fullSession.Player.Inventory.Cards.Count == inventoryBefore;
    }

    private static bool CheckSpecialValueCoefficient()
    {
        return CardValueCalculator.CalculateInitialValue(new EconomyCardDefinition(CardSize.Medium, 4), 3) == 32;
    }

    private static bool CheckAcquisitionSources()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var definition = new EconomyCardDefinition(CardSize.Small);
        var session = new MatchSession(1);

        var purchased = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Purchase);
        var dropped = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Drop);
        var rewarded = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward);

        return purchased.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1
            && dropped.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1
            && rewarded.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1;
    }

    private static bool CheckAcquiredValueRounding() => CardValueCalculator.CalculateAcquiredValue(3) == 1;

    private static bool CheckLevelDoesNotRecalculateValue()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1);
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Reward).Value!;

        card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 4);
        return card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 1;
    }

    private static bool CheckCardMergeUpgrade()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var economy = new CardEconomyService(factory, board);
        var definition = new LightCavalryCardDefinition();
        var session = new MatchSession(1);
        var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(session, first.Card.Id, BoardZone.Battlefield, 0);
        var existingLevelTwo = factory.CreateCard(definition, 2);
        session.Player.Inventory.Add(existingLevelTwo);
        _ = board.PlaceCard(session, existingLevelTwo.Id, BoardZone.Battlefield, 2);
        var merged = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
        var location = session.Board.Locate(first.Card.Id);
        var firstLevelFour = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!;
        var secondLevelFour = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!;

        return merged.WasUpgraded
            && merged.Card.Id == first.Card.Id
            && merged.PreviousLevel == 1
            && merged.CurrentLevel == 3
            && merged.Card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && merged.Card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 40
            && merged.Card.Abilities[0].CooldownTicks == 30
            && location?.Zone == BoardZone.Battlefield
            && location?.Placement.Start == 0
            && session.Player.Inventory.Find(existingLevelTwo.Id) is null
            && firstLevelFour.WasCreated
            && secondLevelFour.WasCreated
            && firstLevelFour.Card.Id != secondLevelFour.Card.Id;
    }

    private static bool CheckCardLevelDefinitions()
    {
        var definition = new JudgmentHammerCardDefinition();
        var factory = new EntityFactory();
        var levelThree = factory.CreateCard(definition);
        var levelFour = factory.CreateCard(definition, 4);
        return definition.InitialLevel == 3
            && !definition.SupportsLevel(1)
            && !definition.SupportsLevel(2)
            && definition.SupportsLevel(3)
            && definition.SupportsLevel(4)
            && !definition.SupportsLevel(5)
            && levelThree.Attributes.Identity.Size == CardSize.Large
            && levelThree.Attributes.Identity.ElementKeys[0] == GameElements.Light
            && levelThree.Tags.Contains(GameTags.Equipment)
            && levelThree.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3
            && levelThree.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.CooldownTicks) == 60
            && levelThree.Abilities[0].CooldownTicks == 60
            && levelFour.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
            && levelFour.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.CooldownTicks) == 50
            && levelFour.Abilities[0].CooldownTicks == 50;
    }

    private static bool CheckBeastHideDefinition()
    {
        var definition = new BeastHideCardDefinition();
        var factory = new EntityFactory();
        var card = factory.CreateCard(definition);
        var registry = DefinitionRegistry.Create([new VerificationHeroDefinition(), definition]);
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        player.Player.Inventory.Add(card);
        _ = CreateBoardService().PlaceCard(player, card.Id, BoardZone.Battlefield, 0);
        var battleSetup = new BattleSetupFactory().Create(player, opponent, 1, new BattleTick(10));
        var economy = new CardEconomyService(factory);
        var levelOne = economy.AcquireCard(new MatchSession(3), definition, 1, CardAcquisitionSource.Reward).Value!;
        var levelTwo = economy.AcquireCard(new MatchSession(4), definition, 2, CardAcquisitionSource.Reward).Value!;
        var levelThree = economy.AcquireCard(new MatchSession(5), definition, 3, CardAcquisitionSource.Reward).Value!;
        var levelFour = economy.AcquireCard(new MatchSession(6), definition, 4, CardAcquisitionSource.Reward).Value!;
        return registry.Cards.ContainsKey(new StringName("card.beast_hide"))
            && definition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && definition.Attributes.Identity.ElementKeys.Count == 1
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Attributes.Identity.Size == CardSize.Small
            && card.Tags.Contains(GameTags.Small)
            && card.Tags.Contains(GameTags.Material)
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 1
            && card.Abilities.Count == 0
            && battleSetup.Player.Cards.Count == 1
            && !battleSetup.Player.Cards[0].UseLegacyAttack
            && CardValueCalculator.CalculateInitialValue(definition, 1) == 2
            && CardValueCalculator.CalculateInitialValue(definition, 2) == 4
            && CardValueCalculator.CalculateInitialValue(definition, 3) == 8
            && CardValueCalculator.CalculateInitialValue(definition, 4) == 16
            && levelOne.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 3
            && levelTwo.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 6
            && levelThree.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 12
            && levelFour.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 24;
    }

    private static bool CheckDiamondDefinition()
    {
        var definition = new DiamondCardDefinition();
        var session = new MatchSession(7);
        var card = new CardEconomyService(new EntityFactory()).AcquireCard(
            session,
            definition,
            definition.InitialLevel,
            CardAcquisitionSource.Reward).Value!;
        return definition.InitialLevel == 4
            && !definition.SupportsLevel(1)
            && !definition.SupportsLevel(2)
            && !definition.SupportsLevel(3)
            && definition.SupportsLevel(4)
            && !definition.SupportsLevel(5)
            && definition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && definition.Attributes.Identity.Size == CardSize.Small
            && definition.Attributes.Identity.ElementKeys.Count == 1
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Tags.Contains(GameTags.Small)
            && definition.Tags.Contains(GameTags.Material)
            && card.Abilities.Count == 0
            && CardValueCalculator.CalculateInitialValue(definition, 4) == 16
            && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 28;
    }

    private static bool CheckJewelryBagSaleReward()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var bagDefinition = new JewelryBagCardDefinition();
        var beastHideDefinition = new BeastHideCardDefinition();
        var definitions = new CardDefinition[]
        {
            bagDefinition,
            beastHideDefinition,
            new DiamondCardDefinition(),
        };
        var economy = new CardEconomyService(factory, board, definitions);

        var session = new MatchSession(11);
        var bag = economy.AcquireCard(session, bagDefinition, 2, CardAcquisitionSource.Reward).Value!;
        var sale = economy.SellCard(session, bag.Id);
        var rewarded = session.Player.Inventory.Cards.Count == 1
            ? session.Player.Inventory.Cards[0]
            : null;

        var fullSession = new MatchSession(12, boardCapacity: 1);
        var blocker = economy.AcquireCard(fullSession, beastHideDefinition, 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(fullSession, blocker.Id, BoardZone.Battlefield, 0);
        var benchBlocker = economy.AcquireCard(fullSession, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!;
        _ = board.PlaceCard(fullSession, benchBlocker.Id, BoardZone.Bench, 0);
        var fullBag = economy.AcquireCard(fullSession, bagDefinition, 2, CardAcquisitionSource.Reward).Value!;
        var fullSale = economy.SellCard(fullSession, fullBag.Id);
        var canRewardDiamondAtLevelFour = false;
        for (ulong seed = 1; seed <= 32 && !canRewardDiamondAtLevelFour; seed++)
        {
            var levelFourSession = new MatchSession(seed);
            var levelFourBag = economy.AcquireCard(
                levelFourSession,
                bagDefinition,
                4,
                CardAcquisitionSource.Reward).Value!;
            _ = economy.SellCard(levelFourSession, levelFourBag.Id);
            canRewardDiamondAtLevelFour = levelFourSession.Player.Inventory.Cards.Count == 1
                && levelFourSession.Player.Inventory.Cards[0].Attributes.Identity.Key
                    == new StringName("card.diamond");
        }

        return bagDefinition.InitialLevel == 2
            && !bagDefinition.SupportsLevel(1)
            && bagDefinition.SupportsLevel(2)
            && bagDefinition.SupportsLevel(3)
            && bagDefinition.SupportsLevel(4)
            && !bagDefinition.SupportsLevel(5)
            && bagDefinition.Tags.Count == 1
            && bagDefinition.Tags.Contains(GameTags.Small)
            && bagDefinition.OnSellReward is RandomTaggedCardOnSellDefinition
            && sale.IsSuccess
            && session.Player.Wealth == 2
            && rewarded is not null
            && rewarded.Attributes.Identity.Key == new StringName("card.beast_hide")
            && rewarded.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2
            && session.Board.Battlefield.Count == 1
            && session.Board.Bench.Count == 0
            && fullSale.IsSuccess
            && fullSession.Player.Wealth == 2
            && fullSession.Player.Inventory.Cards.Count == 2
            && fullSession.Player.Inventory.Find(blocker.Id) is not null
            && fullSession.Player.Inventory.Find(benchBlocker.Id) is not null
            && canRewardDiamondAtLevelFour;
    }

    private static bool CheckTreasureChestSaleReward()
    {
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var chestDefinition = new TreasureChestCardDefinition();
        CardDefinition[] definitions =
        [
            chestDefinition,
            new BeastHideCardDefinition(),
            new DiamondCardDefinition(),
        ];
        var economy = new CardEconomyService(factory, board, definitions);
        var session = new MatchSession(21);
        var chest = economy.AcquireCard(session, chestDefinition, 4, CardAcquisitionSource.Reward).Value!;
        var sale = economy.SellCard(session, chest.Id);
        var reward = chestDefinition.OnSellReward as RandomTaggedCardOnSellDefinition;

        return chestDefinition.InitialLevel == 3
            && !chestDefinition.SupportsLevel(2)
            && chestDefinition.SupportsLevel(3)
            && chestDefinition.SupportsLevel(4)
            && !chestDefinition.SupportsLevel(5)
            && chestDefinition.Attributes.Identity.FactionKey == GameFactions.Neutral
            && chestDefinition.Attributes.Identity.Size == CardSize.Medium
            && chestDefinition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && reward?.Count == 3
            && reward.RequiredSize == CardSize.Small
            && reward.SameLevel
            && sale.IsSuccess
            && session.Player.Inventory.Cards.Count == 3
            && session.Player.Inventory.Cards.All(card =>
                card.Tags.Contains(GameTags.Material)
                && card.Attributes.Identity.Size == CardSize.Small
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4)
            && session.Board.Battlefield.Count == 3;
    }

    private static bool CheckPurchase()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 10);
        var offer = ShopOffer.Create(new EconomyCardDefinition(CardSize.Medium));
        var result = economy.BuyCard(session, offer);
        var repeated = economy.BuyCard(session, offer);

        return result.IsSuccess
            && offer.Price == 4
            && session.Player.Wealth == 6
            && session.Player.Inventory.Cards.Count == 1
            && result.Value?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) == 2
            && repeated.IsFailure
            && offer.IsSold;
    }

    private static bool CheckInsufficientWealth()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 1);
        var result = economy.BuyCard(session, ShopOffer.Create(new EconomyCardDefinition(CardSize.Medium)));

        return result.IsFailure && session.Player.Wealth == 1 && session.Player.Inventory.Cards.Count == 0;
    }

    private static bool CheckSale()
    {
        var economy = new CardEconomyService(new EntityFactory());
        var session = new MatchSession(1, 3);
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Drop).Value!;
        card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 7);

        var firstSale = economy.SellCard(session, card.Id);
        var secondSale = economy.SellCard(session, card.Id);
        return firstSale.IsSuccess
            && firstSale.Value == 7
            && session.Player.Wealth == 10
            && session.Player.Inventory.Cards.Count == 0
            && secondSale.IsFailure;
    }

    private static bool CheckDirectPlacement()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Medium);
        var result = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 3);
        var placement = session.Board.Battlefield.Find(card.Id);
        return result.IsSuccess && placement?.Start == 3 && placement.Size == 2;
    }

    private static bool CheckPushDirections()
    {
        var service = CreateBoardService();
        var rightSession = new MatchSession(1);
        var rightBlocker = AcquireBoardCard(rightSession, CardSize.Small);
        var rightMover = AcquireBoardCard(rightSession, CardSize.Small);
        _ = service.PlaceCard(rightSession, rightBlocker.Id, BoardZone.Battlefield, 0);
        var right = service.PlaceCard(rightSession, rightMover.Id, BoardZone.Battlefield, 0);

        var leftSession = new MatchSession(2);
        var first = AcquireBoardCard(leftSession, CardSize.Small);
        var second = AcquireBoardCard(leftSession, CardSize.Small);
        var leftMover = AcquireBoardCard(leftSession, CardSize.Small);
        _ = service.PlaceCard(leftSession, first.Id, BoardZone.Battlefield, 4);
        _ = service.PlaceCard(leftSession, second.Id, BoardZone.Battlefield, 5);
        var left = service.PlaceCard(leftSession, leftMover.Id, BoardZone.Battlefield, 4);

        return right.Value?.Direction == PushDirection.Right
            && rightSession.Board.Battlefield.Find(rightBlocker.Id)?.Start == 1
            && left.Value?.Direction == PushDirection.Left
            && leftSession.Board.Battlefield.Find(first.Id)?.Start == 3;
    }

    private static bool CheckRightTieBreak()
    {
        var session = new MatchSession(1);
        var blocker = AcquireBoardCard(session, CardSize.Small);
        var mover = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Battlefield, 4);
        var result = service.PlaceCard(session, mover.Id, BoardZone.Battlefield, 4);
        return result.Value?.Direction == PushDirection.Right
            && result.Value.TotalDistance == 1
            && result.Value.AffectedCards == 1
            && session.Board.Battlefield.Find(blocker.Id)?.Start == 5;
    }

    private static bool CheckUnpushableCard()
    {
        var session = new MatchSession(1);
        var blocker = AcquireBoardCard(session, CardSize.Medium);
        var mover = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Battlefield, 4);
        _ = service.SetPushable(session, blocker.Id, false);
        var result = service.PlaceCard(session, mover.Id, BoardZone.Battlefield, 4);
        return result.IsFailure
            && session.Board.Battlefield.Find(blocker.Id)?.Start == 4
            && session.Board.Battlefield.Find(mover.Id) is null;
    }

    private static bool CheckSameZoneMove()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Large);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 2);
        var result = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 3);
        return result.IsSuccess
            && session.Board.Battlefield.Count == 1
            && session.Board.Battlefield.Find(card.Id)?.Start == 3;
    }

    private static bool CheckCrossZoneRollback()
    {
        var session = new MatchSession(1);
        var moving = AcquireBoardCard(session, CardSize.Medium);
        var blocker = AcquireBoardCard(session, CardSize.Medium);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, moving.Id, BoardZone.Battlefield, 2);
        _ = service.PlaceCard(session, blocker.Id, BoardZone.Bench, 4);
        _ = service.SetPushable(session, blocker.Id, false);
        var result = service.PlaceCard(session, moving.Id, BoardZone.Bench, 4);

        return result.IsFailure
            && session.Board.Battlefield.Find(moving.Id)?.Start == 2
            && session.Board.Bench.Find(blocker.Id)?.Start == 4
            && session.Board.Bench.Find(moving.Id) is null;
    }

    private static bool CheckUniqueBoardLocation()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Small);
        var service = CreateBoardService();
        _ = service.PlaceCard(session, card.Id, BoardZone.Battlefield, 1);
        var moved = service.PlaceCard(session, card.Id, BoardZone.Bench, 7);
        return moved.IsSuccess
            && session.Board.Battlefield.Find(card.Id) is null
            && session.Board.Bench.Find(card.Id)?.Start == 7;
    }

    private static bool CheckBoardCapacity()
    {
        var session = new MatchSession(1, boardCapacity: 10);
        var card = AcquireBoardCard(session, CardSize.Large);
        var result = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 8);
        return result.IsFailure && session.Board.Battlefield.Count == 0;
    }

    private static bool CheckRemoveThenSell()
    {
        var session = new MatchSession(1);
        var economy = new CardEconomyService(new EntityFactory());
        var card = economy.AcquireCard(
            session,
            new EconomyCardDefinition(CardSize.Small),
            1,
            CardAcquisitionSource.Reward).Value!;
        var board = CreateBoardService();
        _ = board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var blockedSale = economy.SellCard(session, card.Id);
        var removed = board.RemoveFromBoard(session, card.Id);
        var sale = economy.SellCard(session, card.Id);
        return blockedSale.IsFailure && removed.IsSuccess && sale.IsSuccess;
    }

    private static bool CheckBattleSnapshotIsolation()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var setup = new BattleSetupFactory().Create(player, opponent, 9, new BattleTick(300));
        player.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1);
        var result = new CombatSimulator().Simulate(setup);
        return setup.Player.Hero.MaxHealth == 100
            && result.PlayerRemainingHealth == 0
            && player.Player.Hero.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) == 1;
    }

    private static bool CheckDeterministicBattle()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var setup = new BattleSetupFactory().Create(player, opponent, 99, new BattleTick(300));
        var first = new CombatSimulator().Simulate(setup);
        var second = new CombatSimulator().Simulate(setup);
        return first.Outcome == second.Outcome
            && first.EndedAt == second.EndedAt
            && string.Join("|", first.Events) == string.Join("|", second.Events);
    }

    private static bool CheckFirstActivationTiming()
    {
        var (player, opponent) = CreateBattlePair(true, false);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage)
            {
                return damage.Tick.Value == 10;
            }
        }

        return false;
    }

    private static bool CheckStableFifoOrder()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        var targets = new List<SideId>();
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage && damage.Tick.Value == 10)
            {
                targets.Add(damage.TargetSide);
            }
        }

        return targets.Count == 2 && targets[0] == SideId.Opponent && targets[1] == SideId.Player;
    }

    private static bool CheckArmorDamage()
    {
        var playerHero = new HeroBattleSetup(EntityId.New(), 100, 0);
        var opponentHero = new HeroBattleSetup(EntityId.New(), 100, 10);
        var playerCard = new CardBattleSetup(EntityId.New(), 0, 25, 1);
        var setup = new BattleSetup(
            new BattleSideSetup(playerHero, [playerCard]),
            new BattleSideSetup(opponentHero, Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(1));
        var result = new CombatSimulator().Simulate(setup);
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage)
            {
                return damage.ArmorAbsorbed == 10
                    && damage.HealthDamage == 15
                    && damage.RemainingHealth == 85;
            }
        }

        return false;
    }

    private static bool CheckMaxHealthPercentDamage()
    {
        var ability = new AbilityDefinition(
            new StringName("test.percent_damage"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new MaxHealthPercentDamageEffectDefinition(20)]);
        var result = SimulateAbilities([ability], timeout: 1, opponentArmor: 10, opponentMaxHealth: 103);
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage
                && damage.TargetSide == SideId.Opponent
                && damage.RawDamage == 20)
            {
                return damage.ArmorAbsorbed == 10
                    && damage.HealthDamage == 10
                    && damage.RemainingHealth == 93;
            }
        }

        return false;
    }

    private static bool CheckBoarCard()
    {
        var definition = new BoarCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.boar")
            || identity.FactionKey != GameFactions.Neutral
            || identity.Size != CardSize.Medium
            || identity.ElementKeys.Count != 1
            || identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Beast)
            || TagDisplayNames.Get(GameTags.Beast) != "野兽"
            || definition.InitialLevel != 1
            || definition.SupportsLevel(5))
            return false;

        var expectedDamage = new[] { 20, 30, 50, 100 };
        for (var level = 1; level <= 4; level++)
        {
            var levelDefinition = definition.GetLevel(level);
            if (levelDefinition is null
                || levelDefinition.BaseCombatValues[GameAttributeKeys.AttackDamage] != expectedDamage[level - 1]
                || levelDefinition.Abilities.Count != 1
                || levelDefinition.Abilities[0].Activation != AbilityActivation.Active
                || levelDefinition.Abilities[0].CooldownTicks != 60
                || levelDefinition.Abilities[0].Effects[0]
                    is not SourceHeroHealthScaledAttributeDamageEffectDefinition { BypassArmor: false })
                return false;

            var boarId = EntityId.New();
            var opponentId = EntityId.New();
            var opponentAttack = new AbilityDefinition(
                new StringName("test.boar_opponent_attack"),
                AbilityActivation.Active,
                AbilityTarget.EnemyHero,
                0,
                40,
                [new DamageEffectDefinition(25)]);
            var setup = new BattleSetup(
                new BattleSideSetup(
                    new HeroBattleSetup(EntityId.New(), 100, 0),
                    [new CardBattleSetup(boarId, 0, expectedDamage[level - 1], 60,
                        levelDefinition.Abilities, UseLegacyAttack: false, Tags: definition.Tags,
                        OccupiedSlots: 2, ElementKeys: identity.ElementKeys)]),
                new BattleSideSetup(
                    new HeroBattleSetup(EntityId.New(), 1000, 3),
                    [new CardBattleSetup(opponentId, 0, 50, 40,
                        [opponentAttack], UseLegacyAttack: false)]),
                1,
                new BattleTick(121),
                new BattleTick(1000));
            var result = new CombatSimulator().Simulate(setup);
            var damage = result.Events.OfType<DamageDealtEvent>()
                .Where(value => value.SourceCardId == boarId).ToArray();
            var firstDamage = expectedDamage[level - 1] * 75 / 100;
            var secondDamage = expectedDamage[level - 1] / 2;
            if (damage.Length != 2
                || damage[0].Tick.Value != 60
                || damage[0].TargetSide != SideId.Opponent
                || damage[0].RawDamage != firstDamage
                || damage[0].ArmorAbsorbed != 3
                || damage[0].HealthDamage != firstDamage - 3
                || damage[1].Tick.Value != 120
                || damage[1].TargetSide != SideId.Opponent
                || damage[1].RawDamage != secondDamage
                || damage[1].ArmorAbsorbed != 0
                || damage[1].HealthDamage != secondDamage)
                return false;
        }

        return true;
    }

    private static bool CheckLightCavalry()
    {
        var definition = new LightCavalryCardDefinition();
        var identity = definition.Attributes.Identity;
        var level = definition.GetLevel(1)!;
        if (identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium
            || identity.ElementKeys.Count != 1
            || identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 1
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || level.BaseCombatValues[GameAttributeKeys.AttackDamage] != 10)
        {
            return false;
        }

        var cavalryId = EntityId.New();
        var attackerId = EntityId.New();
        var inertHumanId = EntityId.New();
        var attackerAbility = new AbilityDefinition(
            new StringName("test.attribute_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            40,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
        var humanTags = new TagSet([GameTags.Human]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0),
                [
                    new CardBattleSetup(cavalryId, 0, 10, 30, level.Abilities, UseLegacyAttack: false, Tags: humanTags),
                    new CardBattleSetup(attackerId, 2, 5, 40, [attackerAbility], UseLegacyAttack: false, Tags: humanTags),
                    new CardBattleSetup(inertHumanId, 3, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false, Tags: humanTags),
                ]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(81),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var cavalryActivationTicks = new List<long>();
        var cavalryDamage = new List<int>();
        var cavalryTargetsCorrect = true;
        var attackerDamage = new List<int>();
        var changedCards = new List<EntityId>();

        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent activation && activation.SourceCardId == cavalryId)
                cavalryActivationTicks.Add(activation.Tick.Value);
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == cavalryId)
            {
                cavalryDamage.Add(damage.RawDamage);
                cavalryTargetsCorrect &= damage.TargetSide == SideId.Opponent;
            }
            if (battleEvent is DamageDealtEvent damageByAttacker && damageByAttacker.SourceCardId == attackerId)
                attackerDamage.Add(damageByAttacker.RawDamage);
            if (battleEvent is CardAttributeChangedEvent changed)
                changedCards.Add(changed.CardId);
        }

        return string.Join(",", cavalryActivationTicks) == "30,80"
            && string.Join(",", cavalryDamage) == "10,20"
            && cavalryTargetsCorrect
            && string.Join(",", attackerDamage) == "15,25"
            && changedCards.Count == 4
            && !changedCards.Contains(inertHumanId);
    }

    private static bool CheckArmguard()
    {
        var definition = new ArmguardCardDefinition();
        var level = definition.GetLevel(1)!;
        if (definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 1
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Multicast) != 1)
        {
            return false;
        }

        var armguardId = EntityId.New();
        var opponentCardId = EntityId.New();
        var opponentAttack = new AbilityDefinition(
            new StringName("test.armguard_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            51,
            [new DamageEffectDefinition(20)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(
                    armguardId,
                    0,
                    5,
                    50,
                    level.Abilities,
                    UseLegacyAttack: false,
                    Tags: definition.Tags,
                    Multicast: 1,
                    ArmorAmount: level.BaseCombatValues[GameAttributeKeys.Armor])]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(opponentCardId, 0, 20, 51, [opponentAttack], UseLegacyAttack: false)]),
            1,
            new BattleTick(52),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var armguardActivations = 0;
        var armguardDamage = 0;
        var absorbed = -1;
        var healthDamage = -1;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent activation
                && activation.SourceCardId == armguardId
                && activation.Tick.Value == 50)
            {
                armguardActivations++;
            }
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == armguardId)
                armguardDamage += damage.RawDamage;
            if (battleEvent is DamageDealtEvent attack && attack.SourceCardId == opponentCardId)
            {
                absorbed = attack.ArmorAbsorbed;
                healthDamage = attack.HealthDamage;
            }
        }

        return armguardActivations == 2
            && armguardDamage == 10
            && absorbed == 10
            && healthDamage == 10;
    }

    private static bool CheckArcaneShield()
    {
        var definition = new ArcaneShieldCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Equipment)
            || levelTwo.Abilities[0].CooldownTicks != 80
            || levelThree.Abilities[0].CooldownTicks != 70
            || levelFour.Abilities[0].CooldownTicks != 60
            || levelTwo.Abilities[0].ManaCost != 20
            || levelThree.Abilities[0].ManaCost != 40
            || levelFour.Abilities[0].ManaCost != 80)
        {
            return false;
        }

        var spenderId = EntityId.New();
        var shieldId = EntityId.New();
        var attackerId = EntityId.New();
        var spender = new AbilityDefinition(
            new StringName("test.mana_spender"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            10,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var attack = new AbilityDefinition(
            new StringName("test.arcane_shield_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            81,
            [new DamageEffectDefinition(100)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, InitialMana: 100, ManaRegen: 0),
                [
                    new CardBattleSetup(spenderId, 0, 0, 1, [spender], UseLegacyAttack: false),
                    new CardBattleSetup(shieldId, 1, 0, 80, levelTwo.Abilities, UseLegacyAttack: false),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [new CardBattleSetup(attackerId, 0, 100, 81, [attack], UseLegacyAttack: false)]),
            1,
            new BattleTick(82),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var manaSpent = 0;
        var shieldActivatedAtExpectedTick = false;
        var absorbedExpectedArmor = false;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is ManaChangedEvent mana && mana.Side == SideId.Player)
                manaSpent -= mana.Amount;
            if (battleEvent is AbilityActivatedEvent activation
                && activation.SourceCardId == shieldId
                && activation.Tick.Value == 80)
            {
                shieldActivatedAtExpectedTick = true;
            }
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == attackerId)
                absorbedExpectedArmor = damage.ArmorAbsorbed == 30 && damage.HealthDamage == 70;
        }

        return manaSpent == 30 && shieldActivatedAtExpectedTick && absorbedExpectedArmor;
    }

    private static bool CheckMilitaryBoots()
    {
        var definition = new MilitaryBootsCardDefinition();
        var levelOne = definition.GetLevel(1)!;
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var levelOneEffect = levelOne.Abilities[0].Effects[0]
            as ApplyStatusToAdjacentAlliedCardsEffectDefinition;
        if (definition.InitialLevel != 1
            || !definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Equipment)
            || levelOne.Abilities[0].ManaCost != 0
            || levelOne.Abilities[0].CooldownTicks != 50
            || levelOneEffect is null
            || levelOneEffect.Amount != 10
            || levelOneEffect.BonusTag != GameTags.Human
            || levelOneEffect.BonusMultiplier != 2
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelTwo.Abilities[0].Effects[0]).Amount != 20
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelThree.Abilities[0].Effects[0]).Amount != 30
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelFour.Abilities[0].Effects[0]).Amount != 40)
        {
            return false;
        }

        var humanId = EntityId.New();
        var bootsId = EntityId.New();
        var generalId = EntityId.New();
        var distantId = EntityId.New();
        var marker = new AbilityDefinition(
            new StringName("test.haste_marker"),
            AbilityActivation.Active,
            AbilityTarget.AlliedHero,
            0,
            80,
            [new ArmorEffectDefinition(0)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(
                        humanId,
                        0,
                        0,
                        80,
                        [marker],
                        UseLegacyAttack: false,
                        Tags: new TagSet([GameTags.Human]),
                        OccupiedSlots: 2),
                    new CardBattleSetup(bootsId, 2, 0, 50, levelOne.Abilities, UseLegacyAttack: false),
                    new CardBattleSetup(generalId, 3, 0, 80, [marker], UseLegacyAttack: false),
                    new CardBattleSetup(distantId, 5, 0, 80, [marker], UseLegacyAttack: false),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(81),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var humanTick = -1L;
        var generalTick = -1L;
        var distantTick = -1L;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not AbilityActivatedEvent activation) continue;
            if (activation.SourceCardId == humanId) humanTick = activation.Tick.Value;
            if (activation.SourceCardId == generalId) generalTick = activation.Tick.Value;
            if (activation.SourceCardId == distantId) distantTick = activation.Tick.Value;
        }

        return humanTick == 65 && generalTick == 70 && distantTick == 80;
    }

    private static bool CheckHolyGriffin()
    {
        var definition = new HolyGriffinCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var activeEffect = levelTwo.Abilities[0].Effects[0]
            as ApplyStatusToAdjacentAlliedCardsEffectDefinition;
        var passiveEffect = levelTwo.Abilities[1].Effects[0]
            as ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Beast)
            || !definition.Tags.Contains(GameTags.Mount)
            || levelTwo.Abilities[0].Activation != AbilityActivation.Active
            || levelTwo.Abilities[0].CooldownTicks != 50
            || activeEffect is null
            || activeEffect.Status != BattleStatus.HasteDuration
            || activeEffect.Amount != 10
            || activeEffect.BonusTag != GameTags.Human
            || activeEffect.BonusMultiplier != 1
            || levelTwo.Abilities[1].Activation != AbilityActivation.PassiveAura
            || passiveEffect is null
            || passiveEffect.Status != BattleStatus.HasteDuration
            || passiveEffect.RequiredTag != GameTags.Human
            || passiveEffect.AttributeKey != GameAttributeKeys.AttackDamage
            || passiveEffect.Amount != 10
            || ((ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)
                levelThree.Abilities[1].Effects[0]).Amount != 20
            || ((ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)
                levelFour.Abilities[1].Effects[0]).Amount != 30)
        {
            return false;
        }

        var bootsDefinition = new MilitaryBootsCardDefinition();
        var bootsId = EntityId.New();
        var humanId = EntityId.New();
        var griffinId = EntityId.New();
        var humanAttack = new AbilityDefinition(
            new StringName("test.holy_griffin_human_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            100,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(
                        bootsId,
                        0,
                        0,
                        50,
                        bootsDefinition.GetLevel(1)!.Abilities,
                        UseLegacyAttack: false,
                        OccupiedSlots: 1),
                    new CardBattleSetup(
                        humanId,
                        1,
                        5,
                        100,
                        [humanAttack],
                        UseLegacyAttack: false,
                        Tags: new TagSet([GameTags.Human]),
                        OccupiedSlots: 1),
                    new CardBattleSetup(
                        griffinId,
                        2,
                        0,
                        50,
                        levelTwo.Abilities,
                        UseLegacyAttack: false,
                        Tags: definition.Tags,
                        OccupiedSlots: 3),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(51),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var bonusEvents = 0;
        var finalAttack = 0;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not CardAttributeChangedEvent changed
                || changed.CardId != humanId
                || changed.AttributeKey != GameAttributeKeys.AttackDamage)
            {
                continue;
            }
            bonusEvents++;
            finalAttack = changed.CurrentValue;
        }

        return bonusEvents == 2 && finalAttack == 25;
    }

    private static bool CheckCathedral()
    {
        var definition = new CathedralCardDefinition();
        var level = definition.GetLevel(4)!;
        var aura = level.Abilities[0];
        var auraEffect = aura.Effects[0] as GrantMulticastToAlliedElementCardsEffectDefinition;
        if (definition.InitialLevel != 4
            || definition.SupportsLevel(1)
            || definition.SupportsLevel(2)
            || definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Location)
            || aura.Activation != AbilityActivation.PassiveAura
            || aura.ManaCost != 0
            || aura.CooldownTicks != 0
            || auraEffect is null
            || auraEffect.ElementKey != GameElements.Light
            || auraEffect.Amount != 1)
        {
            return false;
        }

        var firstCathedralId = EntityId.New();
        var secondCathedralId = EntityId.New();
        var benchCathedralId = EntityId.New();
        var lightCardId = EntityId.New();
        var generalCardId = EntityId.New();
        var attack = new AbilityDefinition(
            new StringName("test.cathedral_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new DamageEffectDefinition(1)]);
        var stackedSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(firstCathedralId, 0, 0, 0, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(secondCathedralId, 3, 0, 0, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(benchCathedralId, 0, 0, 0, level.Abilities,
                        IsOnBench: true, UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(lightCardId, 6, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(generalCardId, 7, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.General]),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(1),
            new BattleTick(1000));
        var stackedResult = new CombatSimulator().Simulate(stackedSetup);
        var lightDamageCount = stackedResult.Events.Count(value =>
            value is DamageDealtEvent damage && damage.SourceCardId == lightCardId);
        var generalDamageCount = stackedResult.Events.Count(value =>
            value is DamageDealtEvent damage && damage.SourceCardId == generalCardId);

        var temporaryCathedralId = EntityId.New();
        var repeatedLightCardId = EntityId.New();
        var destroySelf = new AbilityDefinition(
            new StringName("test.cathedral_destroy_self"),
            AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.SelfCard,
            0,
            0,
            [new DestroyCardEffectDefinition(false)]);
        var removableSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(repeatedLightCardId, 0, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(temporaryCathedralId, 1, 0, 0, [aura, destroySelf],
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(2),
            new BattleTick(1000));
        var removableResult = new CombatSimulator().Simulate(removableSetup);
        var firstTickDamage = removableResult.Events.Count(value =>
            value is DamageDealtEvent damage
            && damage.SourceCardId == repeatedLightCardId
            && damage.Tick.Value == 1);
        var secondTickDamage = removableResult.Events.Count(value =>
            value is DamageDealtEvent damage
            && damage.SourceCardId == repeatedLightCardId
            && damage.Tick.Value == 2);

        return lightDamageCount == 3
            && generalDamageCount == 1
            && firstTickDamage == 2
            && secondTickDamage == 1;
    }

    private static bool CheckBlacksmith()
    {
        var definition = new BlacksmithCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var attackModifier = levelTwo.Abilities[0].Effects[0]
            as ModifyTaggedAlliedCardsAttributeEffectDefinition;
        var armorModifier = levelTwo.Abilities[0].Effects[1]
            as ModifyTaggedAlliedCardsAttributeEffectDefinition;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Location)
            || levelTwo.Abilities[0].ManaCost != 0
            || levelTwo.Abilities[0].CooldownTicks != 60
            || attackModifier is null
            || attackModifier.RequiredTag != GameTags.Equipment
            || attackModifier.AttributeKey != GameAttributeKeys.AttackDamage
            || attackModifier.Amount != 10
            || armorModifier is null
            || armorModifier.RequiredTag != GameTags.Equipment
            || armorModifier.AttributeKey != GameAttributeKeys.Armor
            || armorModifier.Amount != 10
            || ((ModifyTaggedAlliedCardsAttributeEffectDefinition)levelThree.Abilities[0].Effects[0]).Amount != 20
            || ((ModifyTaggedAlliedCardsAttributeEffectDefinition)levelFour.Abilities[0].Effects[0]).Amount != 40)
        {
            return false;
        }

        var blacksmithId = EntityId.New();
        var attributeEquipmentId = EntityId.New();
        var fixedEquipmentId = EntityId.New();
        var shieldId = EntityId.New();
        var enemyAttackerId = EntityId.New();
        var attributeEquipmentAbility = new AbilityDefinition(
            new StringName("test.blacksmith_attribute_equipment"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            61,
            [
                new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
            ]);
        var fixedEquipmentAbility = new AbilityDefinition(
            new StringName("test.blacksmith_fixed_equipment"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            61,
            [new DamageEffectDefinition(1)]);
        var enemyAttack = new AbilityDefinition(
            new StringName("test.blacksmith_enemy_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            81,
            [new DamageEffectDefinition(100)]);
        var shieldDefinition = new ArcaneShieldCardDefinition();
        var shieldLevel = shieldDefinition.GetLevel(2)!;
        var equipmentTags = new TagSet([GameTags.Equipment]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, InitialMana: 100, ManaRegen: 0),
                [
                    new CardBattleSetup(blacksmithId, 0, 0, 60, levelTwo.Abilities, UseLegacyAttack: false),
                    new CardBattleSetup(attributeEquipmentId, 2, 5, 61, [attributeEquipmentAbility],
                        UseLegacyAttack: false, Tags: equipmentTags, ArmorAmount: 5),
                    new CardBattleSetup(fixedEquipmentId, 3, 99, 61, [fixedEquipmentAbility],
                        UseLegacyAttack: false, Tags: equipmentTags, ArmorAmount: 99),
                    new CardBattleSetup(shieldId, 4, 0, 80, shieldLevel.Abilities,
                        UseLegacyAttack: false, Tags: shieldDefinition.Tags,
                        ElementKeys: [GameElements.Light], ArmorAmount: 0),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(enemyAttackerId, 0, 100, 81, [enemyAttack], UseLegacyAttack: false)]),
            1,
            new BattleTick(82),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var attributeDamage = -1;
        var fixedDamage = -1;
        var absorbedArmor = -1;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not DamageDealtEvent damage) continue;
            if (damage.SourceCardId == attributeEquipmentId) attributeDamage = damage.RawDamage;
            if (damage.SourceCardId == fixedEquipmentId) fixedDamage = damage.RawDamage;
            if (damage.SourceCardId == enemyAttackerId) absorbedArmor = damage.ArmorAbsorbed;
        }

        return attributeDamage == 15
            && fixedDamage == 1
            && absorbedArmor == 45;
    }

    private static bool CheckHolySlashingBlade()
    {
        var definition = new HolySlashingBladeCardDefinition();
        var level = definition.GetLevel(4)!;
        var active = level.Abilities[0];
        var passive = level.Abilities[1];
        if (definition.InitialLevel != 4
            || definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Equipment)
            || definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) != 200
            || active.CooldownTicks != 100
            || active.Effects[1] is not DestroyRandomEnemyCardEffectDefinition
            || passive.Activation != AbilityActivation.PassiveAura
            || passive.Effects[0] is not MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition)
        {
            return false;
        }

        var bladeId = EntityId.New();
        var alliedUndeadId = EntityId.New();
        var enemySmallDemonId = EntityId.New();
        var enemyMediumUndeadId = EntityId.New();
        var enemyLargeDemonId = EntityId.New();
        var alliedSelfDestroy = new AbilityDefinition(
            new StringName("test.allied_undead_self_destroy"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            0,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 5000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(bladeId, 0, 200, 100, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(alliedUndeadId, 3, 0, 1, [alliedSelfDestroy],
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Undead])),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 5000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(enemySmallDemonId, 0, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon])),
                    new CardBattleSetup(enemyMediumUndeadId, 1, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Undead]), OccupiedSlots: 2),
                    new CardBattleSetup(enemyLargeDemonId, 3, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 3),
                ]),
            17,
            new BattleTick(200),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var repeated = new CombatSimulator().Simulate(setup);
        var bladeDamage = result.Events
            .OfType<DamageDealtEvent>()
            .Where(value => value.SourceCardId == bladeId)
            .Select(value => value.RawDamage)
            .ToArray();
        var enemyDestroyed = result.Events
            .OfType<CardDestroyedEvent>()
            .Where(value => value.Side == SideId.Opponent)
            .Select(value => value.CardId)
            .ToArray();
        var repeatedDestroyed = repeated.Events
            .OfType<CardDestroyedEvent>()
            .Where(value => value.Side == SideId.Opponent)
            .Select(value => value.CardId)
            .ToArray();

        return bladeDamage.SequenceEqual([400, 800])
            && enemyDestroyed.Length == 2
            && enemyDestroyed.All(value => value == enemySmallDemonId || value == enemyMediumUndeadId)
            && !enemyDestroyed.Contains(enemyLargeDemonId)
            && enemyDestroyed.SequenceEqual(repeatedDestroyed);
    }

    private static bool CheckOrderCrusader()
    {
        var definition = new OrderCrusaderCardDefinition();
        var level = definition.GetLevel(2)!;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Human)
            || level.BaseCombatValues[GameAttributeKeys.AttackDamage] != 40
            || level.Abilities[0].CooldownTicks != 80
            || level.Abilities[1].Effects[0] is not GrantTagToEnemySizeCardsEffectDefinition
            || level.Abilities[1].Effects[1]
                is not IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition increase
            || increase.Amount != 10)
        {
            return false;
        }

        var crusaderId = EntityId.New();
        var destroyedSmallId = EntityId.New();
        var selfDestroy = new AbilityDefinition(
            new StringName("test.order_crusader_enemy_self_destroy"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            0,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var countSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(crusaderId, 0, 40, 80, level.Abilities,
                    UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 3)]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(destroyedSmallId, 0, 0, 1, [selfDestroy], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 1, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 2, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 2),
                    new CardBattleSetup(EntityId.New(), 4, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 3),
                ]),
            1,
            new BattleTick(80),
            new BattleTick(1000));
        var countResult = new CombatSimulator().Simulate(countSetup);
        var damage = countResult.Events
            .OfType<DamageDealtEvent>()
            .Single(value => value.SourceCardId == crusaderId)
            .RawDamage;

        var trigger = new AbilityDefinition(
            new StringName("test.order_crusader_aura_removal_trigger"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new DamageEffectDefinition(1)]);
        var destroyCrusader = new AbilityDefinition(
            new StringName("test.order_crusader_destroy_aura_source"),
            AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.SelfCard,
            0,
            0,
            [new DestroyCardEffectDefinition(false)]);
        var destroyDemon = new AbilityDefinition(
            new StringName("test.order_crusader_destroy_demon"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            2,
            [new DestroyRandomEnemyCardEffectDefinition([GameTags.Demon], [CardSize.Small])]);
        var expiringCrusaderAbilities = level.Abilities.Concat([destroyCrusader]).ToArray();
        var neutralSmallId = EntityId.New();
        var expirySetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(EntityId.New(), 0, 1, 1, [trigger], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 1, 0, 2, [destroyDemon], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 2, 40, 80, expiringCrusaderAbilities,
                        UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 3),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(neutralSmallId, 0, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false)]),
            1,
            new BattleTick(2),
            new BattleTick(1000));
        var expiryResult = new CombatSimulator().Simulate(expirySetup);

        return damage == 70
            && countResult.Events.OfType<CardDestroyedEvent>()
                .Any(value => value.CardId == destroyedSmallId)
            && !expiryResult.Events.OfType<CardDestroyedEvent>()
                .Any(value => value.CardId == neutralSmallId);
    }

    private static bool CheckBattleTimeout()
    {
        var (player, opponent) = CreateBattlePair(false, false);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        return result.Outcome == BattleOutcome.PlayerVictory
            && result.EndReason == BattleEndReason.SimultaneousDefeat
            && result.EndedAt.Value == 360;
    }

    private static bool CheckInsufficientMana()
    {
        var ability = new AbilityDefinition(new StringName("test.mana"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 20, 1, [new DamageEffectDefinition(50)]);
        var result = SimulateAbilities([ability], timeout: 5);
        foreach (var battleEvent in result.Events)
            if (battleEvent is AbilityActivatedEvent) return false;
        return result.OpponentRemainingHealth == 0 && result.EndReason == BattleEndReason.Extinction;
    }

    private static bool CheckEchoDoesNotChain()
    {
        var active = new AbilityDefinition(new StringName("test.active"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 1, [new DamageEffectDefinition(1)]);
        var echoA = new AbilityDefinition(new StringName("test.echo_a"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.AlliedHero, 0, 0, [new ArmorEffectDefinition(1)]);
        var echoB = new AbilityDefinition(new StringName("test.echo_b"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.AlliedHero, 0, 0, [new ArmorEffectDefinition(1)]);
        var result = SimulateAbilities([active, echoA, echoB], timeout: 1);
        var activated = 0; var echoes = 0;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent abilityEvent)
            {
                activated++;
                if (abilityEvent.IsEcho) echoes++;
            }
        }
        return activated == 3 && echoes == 2;
    }

    private static bool CheckBurnAndPoison()
    {
        var ability = new AbilityDefinition(new StringName("test.status"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 1,
            [new ApplyStatusEffectDefinition(BattleStatus.Burn, 10),
             new ApplyStatusEffectDefinition(BattleStatus.Poison, 8),
             new DestroyCardEffectDefinition(false)]);
        var result = SimulateAbilities([ability], timeout: 11, opponentArmor: 100);
        var burnCorrect = false; var poisonCorrect = false;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not DamageDealtEvent damage || damage.Tick.Value != 10) continue;
            burnCorrect |= damage.RawDamage == 6 && damage.ArmorAbsorbed == 6 && damage.HealthDamage == 0;
            poisonCorrect |= damage.RawDamage == 8 && damage.ArmorAbsorbed == 0 && damage.HealthDamage == 8;
        }
        return burnCorrect && poisonCorrect;
    }

    private static bool CheckPermanentDestroy()
    {
        var ability = new AbilityDefinition(new StringName("test.destroy"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 1, [new DestroyCardEffectDefinition(true)]);
        var result = SimulateAbilities([ability], timeout: 2);
        return result.PermanentChanges.Count == 1 && result.PermanentChanges[0].ChangeType == "Destroy";
    }

    private static bool CheckNormalEncounterChoices()
    {
        var session = new MatchSession(10);
        var result = CreateEncounterScheduler().Generate(session);
        var hasShop = false;
        var keys = new HashSet<StringName>();
        foreach (var choice in result.Value!)
        {
            hasShop |= choice.Kind == EncounterKind.Shop;
            if (choice.Kind is EncounterKind.Monster or EncounterKind.Pvp) return false;
            keys.Add(choice.Key);
        }
        return result.IsSuccess && result.Value!.Count == 3 && hasShop && keys.Count == 3;
    }

    private static bool CheckDeterministicEncounters()
    {
        var first = CreateEncounterScheduler().Generate(new MatchSession(77)).Value!;
        var second = CreateEncounterScheduler().Generate(new MatchSession(77)).Value!;
        if (first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++)
            if (first[index].Key != second[index].Key) return false;
        return true;
    }

    private static bool CheckEncounterSeenHistory()
    {
        var session = new MatchSession(5);
        var choices = CreateEncounterScheduler().Generate(session).Value!;
        foreach (var choice in choices)
            if (!session.EncounterSchedule.SeenKeys.Contains(choice.Key)) return false;
        return true;
    }

    private static bool CheckMonsterTurn()
    {
        var session = new MatchSession(6);
        session.Progress.Turn = 4;
        var choices = CreateEncounterScheduler().Generate(session).Value!;
        return choices.Count == 3
            && choices[0].Kind == EncounterKind.Monster
            && choices[1].Kind == EncounterKind.Monster
            && choices[2].Kind == EncounterKind.Monster;
    }

    private static bool CheckPvpTurnAdvance()
    {
        var session = new MatchSession(8);
        session.Progress.Turn = 8;
        var scheduler = CreateEncounterScheduler();
        var choices = scheduler.Generate(session).Value!;
        var selected = scheduler.Select(session, choices[0].Key);
        return choices.Count == 1
            && choices[0].Kind == EncounterKind.Pvp
            && selected.Value?.RequiresBattle == true
            && session.Progress.Round == 2
            && session.Progress.Turn == 1;
    }

    private static bool CheckEncounterHistoryIsolation()
    {
        var first = new MatchSession(1);
        var second = new MatchSession(1);
        _ = CreateEncounterScheduler().Generate(first);
        return first.EncounterSchedule.SeenKeys.Count > 0 && second.EncounterSchedule.SeenKeys.Count == 0;
    }

    private static EncounterScheduler CreateEncounterScheduler() => new(CreateVerificationRegistry());

    private static bool CheckMonsterLoss()
    {
        var session = new MatchSession(1);
        var battle = new BattleResult(BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated,
            new BattleTick(1), 0, 50, Array.Empty<BattleEvent>());
        _ = CreateMatchResultService().Apply(session, battle, MatchBattleKind.Monster,
            opponent: CreateBoarOpponent());
        return session.Player.Reputation == 10
            && session.Player.Wealth == 1
            && session.Player.Experience == 1
            && session.PendingMonsterRewards.Count == 0
            && session.Status == MatchStatus.InProgress;
    }

    private static bool CheckMonsterReward()
    {
        var session = new MatchSession(1, 3);
        var opponent = CreateBoarOpponent();
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: opponent);
        var pending = session.PendingMonsterRewards.Count == 1 ? session.PendingMonsterRewards[0] : null;
        var repeated = new MatchSession(1, 3);
        _ = CreateMatchResultService().Apply(repeated, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: CreateBoarOpponent());
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        return session.Player.Wealth == 6
            && session.Player.Experience == 2
            && pending is { Kind: MonsterRewardKind.Card, Level: 1 }
            && (pending.Key == new StringName("card.beast_hide") || pending.Key == new StringName("card.boar"))
            && repeated.PendingMonsterRewards.Count == 1
            && repeated.PendingMonsterRewards[0].Key == pending.Key
            && claimed.IsSuccess
            && session.PendingMonsterRewards.Count == 0
            && session.Player.Inventory.Cards.Count == 1
            && session.Board.Battlefield.Count == 1;
    }

    private static bool CheckMonsterSkillReward()
    {
        var factory = new EntityFactory();
        var skill = new AssaultSkillDefinition();
        var monster = new SkillOnlyVerificationMonsterDefinition();
        var registry = DefinitionRegistry.Create([monster, skill]);
        var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(2, monster);
        var session = new MatchSession(1);
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: opponent);
        var pending = session.PendingMonsterRewards.Count == 1 ? session.PendingMonsterRewards[0] : null;
        var board = CreateBoardService();
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        var genericPassed = pending is { Kind: MonsterRewardKind.Skill, Level: 1 }
            && pending.Key == new StringName("skill.assault")
            && opponent.Player.Skills.Items.Count == 1
            && claimed.IsSuccess
            && session.Player.Skills.Items.Count == 1
            && session.Board.Battlefield.Count == 0
            && session.PendingMonsterRewards.Count == 0;
        if (!genericPassed) return false;

        var boarRegistry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var boar = new LocalTestOpponentProvider(boarRegistry).CreateMonsterOpponent(
            2, boarRegistry.Monsters[new StringName("monster.boar")]);
        var boarSession = new MatchSession(7);
        _ = CreateMatchResultService().Apply(boarSession, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: boar);
        var boarPending = boarSession.PendingMonsterRewards.Count == 1
            ? boarSession.PendingMonsterRewards[0] : null;
        var boarBoard = CreateBoardService();
        var boarClaimed = new MonsterRewardClaimService(boarRegistry,
            new CardEconomyService(factory, boarBoard), new SkillAcquisitionService(factory), boarBoard)
            .ClaimFirst(boarSession);
        return boarPending is { Kind: MonsterRewardKind.Skill, Level: 1 }
            && boarPending.Key == new StringName("skill.charge")
            && boarClaimed.IsSuccess
            && boarSession.Player.Skills.Items.Count == 1
            && boarSession.Player.Skills.Items[0].Attributes.Identity.Key == new StringName("skill.charge");
    }

    private static bool CheckMonsterRewardRetention()
    {
        var session = new MatchSession(1, boardCapacity: 1);
        var factory = new EntityFactory();
        var board = CreateBoardService();
        var blockerDefinition = new BeastHideCardDefinition();
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var blocker = factory.CreateCard(blockerDefinition, 4);
            session.Player.Inventory.Add(blocker);
            if (board.PlaceCard(session, blocker.Id, zone, 0).IsFailure) return false;
        }
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory),
            MatchBattleKind.Monster, opponent: CreateBoarOpponent());
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), blockerDefinition, new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        var claimed = new MonsterRewardClaimService(registry,
            new CardEconomyService(factory, board), new SkillAcquisitionService(factory), board)
            .ClaimFirst(session);
        return claimed.IsFailure
            && session.PendingMonsterRewards.Count == 1
            && session.Player.Inventory.Cards.Count == 2
            && session.Board.Battlefield.Count == 1
            && session.Board.Bench.Count == 1;
    }

    private static MatchSession CreateBoarOpponent()
    {
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        return new LocalTestOpponentProvider(registry).CreateMonsterOpponent(
            2, registry.Monsters[new StringName("monster.boar")]);
    }

    // 验证怪物定义可直接携带技能（表现层验证模块）。
    private sealed class SkillOnlyVerificationMonsterDefinition : MonsterDefinition
    {
        public SkillOnlyVerificationMonsterDefinition()
            : base(new StringName("verification.monster.skill"), "验证技能怪物",
                Array.Empty<MonsterCardEntry>(),
                skills: [new MonsterSkillEntry(new StringName("skill.assault"), 1)])
        {
        }
    }

    private static bool CheckPvpReputationLoss()
    {
        var session = new MatchSession(1);
        session.Progress.Round = 3;
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.OpponentVictory), MatchBattleKind.Pvp, 3);
        return session.Player.Reputation == 7;
    }

    private static bool CheckTenPvpWins()
    {
        var session = new MatchSession(1);
        var service = CreateMatchResultService();
        for (var index = 0; index < 10; index++)
            _ = service.Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory), MatchBattleKind.Pvp);
        return session.Status == MatchStatus.Won && session.Summary?.PvpWins == 10;
    }

    private static bool CheckVictoryPriority()
    {
        var session = new MatchSession(1);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Reputation, 0);
        session.Progress.PvpWins = 9;
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory), MatchBattleKind.Pvp);
        return session.Status == MatchStatus.Won;
    }

    private static bool CheckPermanentChangeApplication()
    {
        var session = new MatchSession(1);
        var card = AcquireBoardCard(session, CardSize.Small);
        _ = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var battle = new BattleResult(BattleOutcome.Draw, BattleEndReason.Timeout, new BattleTick(1), 1, 1,
            Array.Empty<BattleEvent>(), [new PermanentChange(card.Id, "Destroy")]);
        _ = CreateMatchResultService().Apply(session, battle, MatchBattleKind.Monster,
            opponent: CreateBoarOpponent());
        return session.Player.Inventory.Find(card.Id) is null && !session.Board.Contains(card.Id);
    }

    private static bool CheckMatchSnapshot()
    {
        var session = new MatchSession(1, 4);
        _ = AcquireBoardCard(session, CardSize.Small);
        var snapshot = MatchSnapshot.From(session);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 99);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Experience, 42);
        return snapshot.Wealth == 4 && snapshot.Experience == 0 && snapshot.Cards.Count == 1;
    }

    private static MatchResultService CreateMatchResultService() => new(CreateBoardService());

    private static BattleResult CreateBattleResult(BattleOutcome outcome) =>
        new(outcome, BattleEndReason.HeroDefeated, new BattleTick(1), 1, 0, Array.Empty<BattleEvent>());

    private static BattleResult SimulateAbilities(
        IReadOnlyList<AbilityDefinition> abilities,
        int timeout,
        int opponentArmor = 0,
        int opponentMaxHealth = 100)
    {
        var playerHero = new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0);
        var opponentHero = new HeroBattleSetup(EntityId.New(), opponentMaxHealth, opponentArmor, ManaRegen: 0);
        var card = new CardBattleSetup(EntityId.New(), 0, 0, 1, abilities);
        return new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(playerHero, [card]),
            new BattleSideSetup(opponentHero, Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(timeout),
            new BattleTick(timeout)));
    }

    private static (MatchSession Player, MatchSession Opponent) CreateBattlePair(
        bool playerHasCard,
        bool opponentHasCard)
    {
        var factory = new EntityFactory();
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        AddBattleCardIfNeeded(player, playerHasCard);
        AddBattleCardIfNeeded(opponent, opponentHasCard);
        return (player, opponent);
    }

    private static void AddBattleCardIfNeeded(MatchSession session, bool shouldAdd)
    {
        if (!shouldAdd)
        {
            return;
        }

        var economy = new CardEconomyService(new EntityFactory());
        var card = economy.AcquireCard(
            session,
            new VerificationCardDefinition(),
            1,
            CardAcquisitionSource.Reward).Value!;
        _ = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
    }

    private static BoardService CreateBoardService() => new(new BoardPlacementSolver());

    private static CardInstance AcquireBoardCard(MatchSession session, CardSize size)
    {
        var card = new EntityFactory().CreateCard(new EconomyCardDefinition(size), 1);
        session.Player.Inventory.Add(card);
        return card;
    }

    private static CardIdentityAttributes CreateCardIdentity(
        IEnumerable<StringName> elements,
        CardSize size = CardSize.Small) =>
        new(new StringName("verification.card"), "验证卡牌", new StringName("verification.faction"), size, elements);

    private static bool RejectsElements(IEnumerable<StringName> elements)
    {
        try
        {
            _ = CreateCardIdentity(elements);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool CheckSkillMerge()
    {
        var definition = new VerificationSkillDefinition();
        var session = new MatchSession(1);
        var service = new SkillAcquisitionService(new EntityFactory());
        var first = service.AcquireSkill(session, definition, 1).Value!;
        var id = first.Skill.Id;
        var levelTwo = service.AcquireSkill(session, definition, 1).Value!;
        var levelThree = service.AcquireSkill(session, definition, 2).Value!;
        var levelFour = service.AcquireSkill(session, definition, 3).Value!;
        var extraFour = service.AcquireSkill(session, definition, 4).Value!;
        var directFive = service.AcquireSkill(session, definition, 5).Value!;
        var snapshot = MatchSnapshot.From(session);
        var chainSession = new MatchSession(2);
        var chainThree = service.AcquireSkill(chainSession, definition, 3).Value!;
        _ = service.AcquireSkill(chainSession, definition, 2);
        _ = service.AcquireSkill(chainSession, definition, 1);
        var chain = service.AcquireSkill(chainSession, definition, 1).Value!;
        return first.WasCreated
            && !levelTwo.WasCreated && !levelThree.WasCreated && !levelFour.WasCreated
            && levelFour.Skill.Id == id && levelFour.CurrentLevel == 4
            && extraFour.WasCreated && directFive.WasCreated
            && session.Player.Skills.Items.Count == 3
            && snapshot.Skills.Count == 3
            && levelFour.Skill.Abilities[0].Effects[0] is DamageEffectDefinition { Amount: 20 }
            && chain.CurrentLevel == 4 && chainSession.Player.Skills.Items.Count == 1
            && chainSession.Player.Skills.Items[0].Id == chain.Skill.Id
            && chainSession.Player.Skills.Items[0].Id != chainThree.Skill.Id;
    }

    private static bool CheckSkillBattle()
    {
        var factory = new EntityFactory();
        var matches = new CreateMatchService(factory);
        var hero = new VerificationHeroDefinition();
        var player = matches.Create(1, 0, hero);
        var opponent = matches.Create(2, 0, hero);
        var skill = new SkillAcquisitionService(factory)
            .AcquireSkill(player, new VerificationSkillDefinition(), 1).Value!.Skill;
        var attacker = factory.CreateCard(new VerificationCardDefinition());
        opponent.Player.Inventory.Add(attacker);
        _ = CreateBoardService().PlaceCard(opponent, attacker.Id, BoardZone.Battlefield, 0);
        var setup = new BattleSetupFactory().Create(player, opponent, 99, new BattleTick(12), new BattleTick(300));
        var result = new CombatSimulator().Simulate(setup);
        return player.Board.Battlefield.Count == 0
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
            && result.Events.OfType<AbilityActivatedEvent>().Any(value =>
                value.SourceCardId == skill.Id && value.SourceKind == AbilitySourceKind.Skill && value.IsEcho)
            && result.Events.OfType<DamageDealtEvent>().Any(value =>
                value.SourceCardId == skill.Id && value.SourceKind == DamageSourceKind.Skill
                && value.TargetSide == SideId.Opponent && value.RawDamage == 5)
            && result.Events.OfType<AbilityActivatedEvent>().Count(value => value.SourceCardId == skill.Id) == 1;
    }

    private static bool CheckChargeSkill()
    {
        var definition = new ChargeSkillDefinition();
        var factory = new EntityFactory();
        var multipliers = new[] { 10, 20, 30, 40 };
        var heroLevels = new[] { 1, 3, 5, 2 };
        for (var level = 1; level <= 4; level++)
        {
            var skill = factory.CreateSkill(definition, level);
            var playerCard = new CardBattleSetup(EntityId.New(), 0, 0, 2);
            var opponentCard = new CardBattleSetup(EntityId.New(), 0, 1, 1);
            var setup = new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0, Level: heroLevels[level - 1]),
                    [playerCard], [new SkillBattleSetup(skill.Id, skill.Abilities,
                        skill.Attributes.BaseCombat.SnapshotFinalValues())]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 5), [opponentCard]),
                1, new BattleTick(5), new BattleTick(300));
            var events = new CombatSimulator().Simulate(setup).Events;
            var charge = events.OfType<DamageDealtEvent>()
                .Where(value => value.SourceCardId == skill.Id).ToArray();
            if (charge.Length != 1
                || charge[0].SourceKind != DamageSourceKind.Skill
                || charge[0].TargetSide != SideId.Opponent
                || charge[0].RawDamage != heroLevels[level - 1] * multipliers[level - 1]
                || charge[0].ArmorAbsorbed != 5
                || charge[0].Tick != new BattleTick(2)
                || skill.Abilities[0].Effects[0] is not SourceHeroLevelScaledDamageEffectDefinition levelEffect
                || levelEffect.Multiplier != multipliers[level - 1]
                || events.OfType<AbilityActivatedEvent>().Count(value =>
                    value.SourceCardId == skill.Id && value.IsEcho) != 1
                || events.OfType<AbilityActivatedEvent>().Count(value =>
                    value.SourceCardId == playerCard.EntityId) < 2
                || !events.OfType<AbilityActivatedEvent>().Any(value =>
                    value.SourceCardId == opponentCard.EntityId && value.Tick == new BattleTick(1)))
                return false;
        }

        var idleSkill = factory.CreateSkill(definition);
        var idleSetup = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0), [],
                [new SkillBattleSetup(idleSkill.Id, idleSkill.Abilities,
                    idleSkill.Attributes.BaseCombat.SnapshotFinalValues())]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 200, 0),
                [new CardBattleSetup(EntityId.New(), 0, 1, 1)]),
            2, new BattleTick(2), new BattleTick(300));
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        player.Player.Hero!.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 7);
        return !new CombatSimulator().Simulate(idleSetup).Events.OfType<AbilityActivatedEvent>()
                .Any(value => value.SourceCardId == idleSkill.Id)
            && new BattleSetupFactory().Create(player, opponent, 1, new BattleTick(1)).Player.Hero.Level == 7;
    }

    private static bool CheckUnknownCardSet()
    {
        try
        {
            _ = DefinitionRegistry.Create([new VerificationSetCardDefinition("verification.set_card_a")]);
            return false;
        }
        catch (DefinitionValidationException)
        {
            return true;
        }
    }

    private static bool CheckCardSetBonuses()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationCardSetDefinition(),
            new VerificationSetCardDefinition("verification.set_card_a"),
            new VerificationSetCardDefinition("verification.set_card_b"),
            new VerificationSetCardDefinition("verification.set_card_c"),
        ]);
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var opponent = new MatchSession(2);
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var first = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var second = factory.CreateCard(registry.Cards[new StringName("verification.set_card_b")]);
        var duplicate = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var third = factory.CreateCard(registry.Cards[new StringName("verification.set_card_c")]);
        foreach (var card in new[] { first, second, duplicate, third })
            session.Player.Inventory.Add(card);
        if (board.PlaceCard(session, first.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Battlefield, 1).IsFailure
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || board.PlaceCard(session, duplicate.Id, BoardZone.Battlefield, 2).IsFailure
            || duplicate.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || board.PlaceCard(session, third.Id, BoardZone.Battlefield, 3).IsFailure)
            return false;

        var active = new CardSetEvaluator().Evaluate(session, registry.Sets);
        var frozen = new BattleSetupFactory(registry.Sets).Create(session, opponent, 1, new BattleTick(10));
        if (active.Count != 2
            || active[0].Threshold.RequiredDistinctCards != 2
            || active[1].Threshold.RequiredDistinctCards != 3
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 65
            || frozen.Player.Cards[0].AttackDamage != 65)
            return false;

        new CardSetBonusService(registry.Sets).Recalculate(session);
        if (first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 65
            || board.PlaceCard(session, third.Id, BoardZone.Bench, 0).IsFailure
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 15
            || third.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 5
            || frozen.Player.Cards[0].AttackDamage != 65)
            return false;

        var independent = new StatModifier(ModifierId.New(), first.Id, GameAttributeKeys.AttackDamage, 7);
        first.Attributes.BaseCombat.ApplyModifier(independent);
        return board.RemoveFromBoard(session, second.Id).IsSuccess
            && new CardSetEvaluator().Evaluate(session, registry.Sets).Count == 0
            && first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 12
            && duplicate.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5;
    }

    private static bool CheckCardSetExplicitTargets()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationCardSetDefinition(explicitTargets: true),
            new VerificationSetCardDefinition("verification.set_card_a"),
            new BeastHideCardDefinition(),
        ]);
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var member = factory.CreateCard(registry.Cards[new StringName("verification.set_card_a")]);
        var other = factory.CreateCard(registry.Cards[new StringName("card.beast_hide")]);
        var bench = factory.CreateCard(registry.Cards[new StringName("card.beast_hide")]);
        foreach (var card in new[] { member, other, bench }) session.Player.Inventory.Add(card);
        return board.PlaceCard(session, member.Id, BoardZone.Battlefield, 0).IsSuccess
            && board.PlaceCard(session, other.Id, BoardZone.Battlefield, 1).IsSuccess
            && board.PlaceCard(session, bench.Id, BoardZone.Bench, 0).IsSuccess
            && member.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 15
            && other.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 10
            && bench.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 0
            && session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 3;
    }

    private static bool CheckCardSetBattleSource()
    {
        var registry = DefinitionRegistry.Create([
            new VerificationBattleSetDefinition(), new VerificationSelfDestroyingSetCardDefinition(),
        ]);
        var factory = new EntityFactory();
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var card = factory.CreateCard(registry.Cards[new StringName("verification.self_destroying_set_card")]);
        player.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        if (board.PlaceCard(player, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var setup = new BattleSetupFactory(registry.Sets).Create(
            player, opponent, 3, new BattleTick(2), new BattleTick(300));
        if (setup.Player.Sets.Count != 1
            || board.PlaceCard(player, card.Id, BoardZone.Bench, 0).IsFailure)
            return false;
        var events = new CombatSimulator().Simulate(setup).Events;
        var destroyedAt = events.ToList().FindIndex(value => value is CardDestroyedEvent);
        var setActivatedAt = events.ToList().FindIndex(value => value is AbilityActivatedEvent
            { SourceKind: AbilitySourceKind.CardSet });
        return player.Board.Battlefield.Count == 0
            && destroyedAt >= 0 && setActivatedAt > destroyedAt
            && events.OfType<DamageDealtEvent>().Any(value =>
                value.SourceKind == DamageSourceKind.CardSet && value.RawDamage == 7)
            && events.OfType<AbilityActivatedEvent>().Count(value =>
                value.SourceKind == AbilitySourceKind.CardSet) == 1;
    }

    private static bool CheckCardQuestProgress()
    {
        var definition = new VerificationQuestCardDefinition();
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var board = new BoardService(new BoardPlacementSolver());
        var first = factory.CreateCard(definition);
        var second = factory.CreateCard(definition);
        var third = factory.CreateCard(definition);
        session.Player.Inventory.Add(first);
        session.Player.Inventory.Add(second);
        if (board.PlaceCard(session, first.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Bench, 0).IsFailure)
            return false;
        var quest = definition.Quests[0];
        if (first.IsQuestUnlocked(quest) || second.IsQuestUnlocked(quest)) return false;

        var victory = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
            BattleTick.Zero, 100, 0, []);
        if (new MatchResultService(board).Apply(session, victory, MatchBattleKind.Pvp).IsFailure
            || !first.IsQuestUnlocked(quest) || !second.IsQuestUnlocked(quest)
            || first.GetQuestProgress(quest.Key) != 1 || second.GetQuestProgress(quest.Key) != 1
            || first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 105
            || second.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 5)
            return false;

        session.Player.Inventory.Add(third);
        if (board.PlaceCard(session, third.Id, BoardZone.Bench, 1).IsFailure) return false;
        var defeat = new BattleResult(BattleOutcome.OpponentVictory, BattleEndReason.HeroDefeated,
            BattleTick.Zero, 0, 100, []);
        if (new MatchResultService(board).Apply(session, defeat, MatchBattleKind.Pvp).IsFailure
            || third.GetQuestProgress(quest.Key) != 0
            || board.PlaceCard(session, first.Id, BoardZone.Bench, 2).IsFailure
            || board.PlaceCard(session, second.Id, BoardZone.Battlefield, 0).IsFailure)
            return false;

        var opponent = new MatchSession(2);
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(2));
        var snapshot = MatchSnapshot.From(session);
        return first.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5
            && second.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 105
            && frozen.Player.Cards[0].AttackDamage == 105
            && snapshot.Cards.Single(value => value.Id == first.Id).Quests[0].Unlocked
            && !snapshot.Cards.Single(value => value.Id == third.Id).Quests[0].Unlocked
            && new MatchResultService(board).Apply(session, victory, MatchBattleKind.Pvp).IsSuccess
            && third.IsQuestUnlocked(quest)
            && third.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 5;
    }

    private static bool CheckCardQuestBattleDestroy()
    {
        var definition = new VerificationQuestSelfDestroyCardDefinition();
        var factory = new EntityFactory();
        var session = new MatchSession(1);
        var opponent = new MatchSession(2);
        session.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        var card = factory.CreateCard(definition);
        session.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver());
        if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var setupFactory = new BattleSetupFactory();
        if (setupFactory.Create(session, opponent, 1, new BattleTick(2)).Player.Cards[0].Abilities!.Count != 1)
            return false;
        new CardQuestService(board).ProcessEvent(session, new BattleWonQuestEvent());
        var setup = setupFactory.Create(session, opponent, 1, new BattleTick(2));
        if (setup.Player.Cards[0].Abilities!.Count != 2) return false;
        var events = new CombatSimulator().Simulate(setup).Events;
        return events.OfType<CardDestroyedEvent>().Any(value => value.CardId == card.Id)
            && events.OfType<DamageDealtEvent>().Count(value => value.SourceCardId == card.Id) == 1
            && !events.OfType<AbilityActivatedEvent>().Any(value => value.SourceCardId == card.Id && value.IsEcho);
    }

    private static DefinitionRegistry CreateVerificationRegistry() => DefinitionRegistry.Create(
    [
        new VerificationHeroDefinition(),
        new VerificationCardDefinition(),
        new VerificationSkillDefinition(),
        new VerificationEncounterDefinition("verification.event", EncounterKind.Other),
        new VerificationEncounterDefinition("verification.shop.primary", EncounterKind.Shop, 2),
        new VerificationEncounterDefinition("verification.shop.secondary", EncounterKind.Shop),
        new VerificationEncounterDefinition("verification.monster.one", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.monster.two", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.monster.three", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.pvp", EncounterKind.Pvp),
    ]);

    // 验证专用英雄定义（表现层验证模块）。
    private sealed class VerificationHeroDefinition : HeroDefinition
    {
        public VerificationHeroDefinition()
            : base(
                new EntityAttributes<HeroIdentityAttributes>(
                    new HeroIdentityAttributes(
                        new StringName("verification.hero"),
                        "验证英雄",
                        new StringName("verification.faction")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.MaxHealth] = 100,
                        [GameAttributeKeys.Armor] = 0,
                        [GameAttributeKeys.MaxMana] = 100,
                        [GameAttributeKeys.Mana] = 0,
                        [GameAttributeKeys.ManaRegen] = 10,
                    })))
        {
        }
    }

    private sealed class VerificationSkillDefinition : SkillDefinition
    {
        public VerificationSkillDefinition()
            : base(
                new EntityAttributes<SkillIdentityAttributes>(
                    new SkillIdentityAttributes(
                        new StringName("verification.skill"),
                        "验证回击",
                        GameFactions.Neutral)),
                initialLevel: 1,
                levels:
                [
                    CreateLevel(1, 5),
                    CreateLevel(2, 10),
                    CreateLevel(3, 15),
                    CreateLevel(4, 20),
                    CreateLevel(5, 25),
                ])
        {
        }

        private static SkillLevelDefinition CreateLevel(int level, int damage) => new(
            level,
            null,
            [new AbilityDefinition(
                new StringName("verification.skill.damage"),
                AbilityActivation.EchoOnDamageDealt,
                AbilityTarget.EnemyHero,
                0,
                0,
                [new DamageEffectDefinition(damage)])]);
    }

    // 验证用战士套装，不进入正式内容注册扫描（表现层验证模块）。
    private sealed class VerificationCardSetDefinition : CardSetDefinition
    {
        public VerificationCardSetDefinition(bool explicitTargets = false)
            : base(
                new EntityAttributes<CardSetIdentityAttributes>(
                    new CardSetIdentityAttributes(new StringName("verification.warrior_set"), "验证战士套装")),
                explicitTargets
                    ? [new CardSetThresholdDefinition(1, [
                        SetModifier("verification.set.all_attack", GameAttributeKeys.AttackDamage, 10, AbilityTarget.AllBattlefieldCards),
                        SetModifier("verification.set.hero_armor", GameAttributeKeys.Armor, 3, AbilityTarget.AlliedHero),
                    ])]
                    : [
                    new CardSetThresholdDefinition(2, [SetModifier("verification.set.two_attack", GameAttributeKeys.AttackDamage, 10)]),
                    new CardSetThresholdDefinition(3, [SetModifier("verification.set.three_attack", GameAttributeKeys.AttackDamage, 50)]),
                    ])
        {
        }

        private static AbilityDefinition SetModifier(
            string key, StringName attributeKey, int amount,
            AbilityTarget target = AbilityTarget.SourceGroupCards) => new(
                new StringName(key), AbilityActivation.PassiveWhileEnabled, target, 0, 0,
                [new ModifyAttributeEffectDefinition(attributeKey, amount)]);
    }

    // 验证套装的战斗回响来源（表现层验证模块）。
    private sealed class VerificationBattleSetDefinition : CardSetDefinition
    {
        public VerificationBattleSetDefinition()
            : base(
                new EntityAttributes<CardSetIdentityAttributes>(
                    new CardSetIdentityAttributes(new StringName("verification.battle_set"), "验证战斗套装")),
                [new CardSetThresholdDefinition(1, [new AbilityDefinition(
                    new StringName("verification.battle_set_echo"), AbilityActivation.EchoOnDamageDealt,
                    AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(7)])])])
        {
        }
    }

    // 验证卡牌在造成伤害前摧毁自身（表现层验证模块）。
    private sealed class VerificationSelfDestroyingSetCardDefinition : CardDefinition
    {
        public VerificationSelfDestroyingSetCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.self_destroying_set_card"), "验证自毁套装卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General],
                        new StringName("verification.battle_set")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(
                    new StringName("verification.self_destroying_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 1,
                    [new DestroyCardEffectDefinition(false), new DamageEffectDefinition(1)])])
        {
        }
    }

    // 验证任务数值解锁与实例独立进度的卡牌（表现层验证模块）。
    private sealed class VerificationQuestCardDefinition : CardDefinition
    {
        public VerificationQuestCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.quest_card"), "验证任务卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 5,
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(new StringName("verification.quest_attack"),
                    AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 1,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
                quests: [new CardQuestDefinition(
                    new StringName("verification.win_once"), new BattleVictoryQuestConditionDefinition(), 1,
                    [new AbilityDefinition(new StringName("verification.quest_attack_bonus"),
                        AbilityActivation.PassiveWhileEnabled, AbilityTarget.SelfCard, 0, 0,
                        [new ModifyAttributeEffectDefinition(GameAttributeKeys.AttackDamage, 100)])])])
        {
        }
    }

    // 验证任务战斗能力随卡牌摧毁失效（表现层验证模块）。
    private sealed class VerificationQuestSelfDestroyCardDefinition : CardDefinition
    {
        public VerificationQuestSelfDestroyCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.quest_destroy_card"), "验证自毁任务卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(new StringName("verification.quest_destroy_attack"),
                    AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 1,
                    [new DestroyCardEffectDefinition(false), new DamageEffectDefinition(1)])],
                quests: [new CardQuestDefinition(
                    new StringName("verification.destroy_win_once"), new BattleVictoryQuestConditionDefinition(), 1,
                    [new AbilityDefinition(new StringName("verification.quest_destroy_echo"),
                        AbilityActivation.EchoOnDamageDealt, AbilityTarget.EnemyHero, 0, 0,
                        [new DamageEffectDefinition(7)])])])
        {
        }
    }

    // 验证用套装卡牌定义（表现层验证模块）。
    private sealed class VerificationSetCardDefinition : CardDefinition
    {
        public VerificationSetCardDefinition(string key)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName(key), "验证套装卡", GameFactions.Neutral, CardSize.Small,
                        [GameElements.General], new StringName("verification.warrior_set")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 5,
                        [GameAttributeKeys.CooldownTicks] = 10,
                    })),
                new TagSet(),
                [new AbilityDefinition(
                    new StringName("verification.set_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 10,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])])
        {
        }
    }

    // 验证专用遭遇定义（表现层验证模块）。
    private sealed class VerificationEncounterDefinition : EncounterDefinition
    {
        public VerificationEncounterDefinition(string key, EncounterKind kind, int baseWeight = 1)
            : base(
                new EntityAttributes<EncounterIdentityAttributes>(
                    new EncounterIdentityAttributes(new StringName(key), "验证遭遇")),
                1,
                99,
                kind,
                baseWeight)
        {
        }
    }

    // 验证专用卡牌定义（表现层验证模块）。
    private sealed class VerificationCardDefinition : CardDefinition
    {
        public VerificationCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.sword"),
                        "验证之剑",
                        new StringName("verification.faction"),
                        CardSize.Small,
                        [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 25,
                        [GameAttributeKeys.CooldownTicks] = 10,
                    })),
                new TagSet([GameTags.Equipment]),
                [
                    new AbilityDefinition(
                        new StringName("verification.basic_attack"),
                        AbilityActivation.Active,
                        AbilityTarget.EnemyHero,
                        0,
                        10,
                        [new DamageEffectDefinition(25)]),
                ])
        {
        }
    }

    private sealed class EconomyCardDefinition : CardDefinition
    {
        private readonly int _valueCoefficient;

        public EconomyCardDefinition(CardSize size, int valueCoefficient = 2)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.economy.{size}.{valueCoefficient}"),
                        "经济验证卡牌",
                        new StringName("verification.faction"),
                        size,
                        [GameElements.General])),
                new TagSet())
        {
            _valueCoefficient = valueCoefficient;
        }

        public override int ValueCoefficient => _valueCoefficient;
    }

    private sealed class ShopVerificationCardDefinition : CardDefinition
    {
        public ShopVerificationCardDefinition(CardSize size, StringName factionKey, string suffix = "default")
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.shop_card.{factionKey}.{size}.{suffix}"),
                        "商店验证卡牌",
                        factionKey,
                        size,
                        [GameElements.General])),
                new TagSet())
        {
        }
    }

    // 5级默认价值验证卡牌定义（表现层验证模块）。
    private sealed class FiveLevelEconomyCardDefinition : CardDefinition
    {
        public FiveLevelEconomyCardDefinition(CardSize size)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.economy.level_five.{size}"),
                        "5级经济验证卡牌",
                        new StringName("verification.faction"),
                        size,
                        [GameElements.General])),
                new TagSet(),
                levels:
                [
                    new CardLevelDefinition(1, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(2, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(3, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(4, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(5, null, Array.Empty<AbilityDefinition>()),
                ])
        {
        }
    }
}
