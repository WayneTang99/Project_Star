using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
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
        ("定义中的初始属性不可修改", CheckFrozenDefinition),
        ("工厂创建完全独立的卡牌实例", CheckIndependentInstances),
        ("新对局可以选择英雄并生成卡牌", CheckMatchCreation),
        ("不同对局之间不共享状态", CheckSessionIsolation),
        ("尺寸与等级按翻倍表计算初始价值", CheckInitialValues),
        ("特殊卡牌可以覆写价值系数", CheckSpecialValueCoefficient),
        ("获得卡牌统一设置半价现值", CheckAcquisitionSources),
        ("半价出现小数时向下取整", CheckAcquiredValueRounding),
        ("等级变化不会重算当前价值", CheckLevelDoesNotRecalculateValue),
        ("卡牌可以限制等级并应用分级配置", CheckCardLevelDefinitions),
        ("无阵营无属性卡牌支持分级价值且没有能力", CheckBeastHideDefinition),
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
        ("移出棋盘后卡牌可以出售", CheckRemoveThenSell),
        ("战斗快照与对局实例隔离", CheckBattleSnapshotIsolation),
        ("相同输入产生相同战斗日志", CheckDeterministicBattle),
        ("首次攻击在完整冷却后发动", CheckFirstActivationTiming),
        ("同 Tick 按玩家方优先进入 FIFO", CheckStableFifoOrder),
        ("伤害先扣护甲再扣生命", CheckArmorDamage),
        ("最大生命百分比伤害向下取整并先扣护甲", CheckMaxHealthPercentDamage),
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
        ("怪物战失败不会扣除声望", CheckMonsterLoss),
        ("怪物战胜利奖励财富", CheckMonsterReward),
        ("PvP 失败按战斗轮数扣声望", CheckPvpReputationLoss),
        ("十次 PvP 胜利结束对局", CheckTenPvpWins),
        ("胜利与声望归零同时满足时胜利优先", CheckVictoryPriority),
        ("永久摧毁同时移除归属记录和棋盘实例", CheckPermanentChangeApplication),
        ("对局快照保留只读结算数据", CheckMatchSnapshot),
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
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.OpponentVictory), MatchBattleKind.Monster);
        return session.Player.Reputation == 10 && session.Status == MatchStatus.InProgress;
    }

    private static bool CheckMonsterReward()
    {
        var session = new MatchSession(1, 3);
        _ = CreateMatchResultService().Apply(session, CreateBattleResult(BattleOutcome.PlayerVictory), MatchBattleKind.Monster);
        return session.Player.Wealth == 5;
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
        _ = CreateMatchResultService().Apply(session, battle, MatchBattleKind.Monster);
        return session.Player.Inventory.Find(card.Id) is null && !session.Board.Contains(card.Id);
    }

    private static bool CheckMatchSnapshot()
    {
        var session = new MatchSession(1, 4);
        _ = AcquireBoardCard(session, CardSize.Small);
        var snapshot = MatchSnapshot.From(session);
        session.Player.Resources.SetBaseValue(GameAttributeKeys.Wealth, 99);
        return snapshot.Wealth == 4 && snapshot.Cards.Count == 1;
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
        var economy = new CardEconomyService(new EntityFactory());
        return economy.AcquireCard(
            session,
            new EconomyCardDefinition(size),
            1,
            CardAcquisitionSource.Reward).Value!;
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

    private static DefinitionRegistry CreateVerificationRegistry() => DefinitionRegistry.Create(
    [
        new VerificationHeroDefinition(),
        new VerificationCardDefinition(),
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
                        [GameElements.General])),
                new TagSet([GameTags.Equipment]))
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
