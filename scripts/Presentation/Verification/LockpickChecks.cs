using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 开锁器出售的分级、永久疾速时长、实际发动及交易隔离验证（表现层）。
internal static class LockpickChecks
{
    internal static bool LevelsAndTiming()
    {
        var definition = new LockpickCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(LockpickChecks).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.Key != new StringName("card.lockpick")
            || identity.DisplayName != "开锁器" || identity.FactionKey != new StringName("valos")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || definition.Tags.Count != 1 || !definition.Tags.Contains(GameTags.Small)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Sell
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var level in Enumerable.Range(2, 3))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var f = Fixture();
            var other = Add(f, new BeastHideCardDefinition(), 1, BoardZone.Battlefield, 0);
            // 先创建右侧卡牌，确保按棋盘位置而非库存顺序选择。
            var right = Add(f, new BaaSheepCardDefinition(), 1, BoardZone.Battlefield, 6);
            var left = Add(f, new BaaSheepCardDefinition(), 1, BoardZone.Battlefield, 2);
            var bench = Add(f, new BaaSheepCardDefinition(), 1, BoardZone.Bench, 0);
            var lockpick = Add(f, definition, level, zone, 8);
            var frozen = Setup(f);
            var wealth = f.Session.Player.Wealth;
            var value = lockpick.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = f.Session.Random.State;
            var amount = (level - 1) * 5;
            if (lockpick.Abilities.Count != 0
                || lockpick.OnSellReward is not IncreaseLeftmostHasteCardOnSellDefinition { AmountTicks: var configured }
                || configured != amount) return false;
            var sale = zone is null ? f.Economy.SellCard(f.Session, lockpick.Id) : f.Economy.SellFromBoard(f.Session, lockpick.Id);
            var next = Setup(f);
            var snapshot = MatchSnapshot.From(f.Session).Cards.Single(card => card.Id == left.Id);
            if (sale.IsFailure || f.Session.Player.Wealth != wealth + value || f.Session.Random.State != random
                || f.Session.Player.Inventory.Find(lockpick.Id) is not null || f.Session.Board.Contains(lockpick.Id)
                || Bonus(left) != amount || Bonus(right) != 0 || Bonus(bench) != 0 || Bonus(other) != 0
                || f.Economy.SellCard(f.Session, lockpick.Id).IsSuccess || Bonus(left) != amount
                || !HasteAmounts(frozen).SequenceEqual(new[] { 10, 10 })
                || !HasteAmounts(next).SequenceEqual(new[] { 10 + amount, 10 })
                || snapshot.CurrentValues[GameAttributeKeys.HasteDurationBonus] != amount
                || !CardDisplayAdapter.Details(snapshot).Contains($"疾速时长加成：基础 0秒 / 当前 {amount / 10m:0.##}秒")
                || !new CombatSimulator().Simulate(next).Events.SequenceEqual(new CombatSimulator().Simulate(next).Events)) return false;
        }
        return true;
    }

    internal static bool StackingAndFailures()
    {
        var f = Fixture();
        var target = Add(f, new BaaSheepCardDefinition(), 1, BoardZone.Battlefield, 2);
        SellLock(f, 2);
        var upgraded = f.Economy.AcquireCard(f.Session, new LockpickCardDefinition(), 2, CardAcquisitionSource.Reward).Value!.Card;
        var merge = f.Economy.AcquireCard(f.Session, new LockpickCardDefinition(), 2, CardAcquisitionSource.Reward).Value!;
        if (merge.Card.Id != upgraded.Id || merge.CurrentLevel != 3
            || f.Economy.SellCard(f.Session, upgraded.Id).IsFailure || Bonus(target) != 15) return false;
        SellLock(f, 4);
        f.Factory.ApplyCardLevel(target, new BaaSheepCardDefinition(), 4);
        if (Bonus(target) != 30 || !HasteAmounts(Setup(f)).SequenceEqual(new[] { 70 })) return false;
        f.Board.PlaceCard(f.Session, target.Id, BoardZone.Bench, 0);
        SellLock(f, 2); // 无战场目标仍出售成功，不强化备战区。
        if (Bonus(target) != 30) return false;
        f.Board.PlaceCard(f.Session, target.Id, BoardZone.Battlefield, 2);
        var overflow = Add(f, new LockpickCardDefinition(), 4, BoardZone.Bench, 4);
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HasteDurationBonus, int.MaxValue - 30);
        var wealth = f.Session.Player.Wealth; var random = f.Session.Random.State;
        if (!FailedSalePreserves()) return false; // 加成本身溢出。
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HasteDurationBonus, int.MaxValue - 45);
        if (!FailedSalePreserves()) return false; // 加成合法，但单次疾速时长溢出。
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HasteDurationBonus, int.MaxValue - 85);
        if (f.Economy.SellFromBoard(f.Session, overflow.Id).IsFailure || Bonus(target) != int.MaxValue - 40
            || !HasteAmounts(Setup(f)).SequenceEqual(new[] { int.MaxValue })) return false;
        var independent = Add(f, new BaaSheepCardDefinition(), 1, BoardZone.Bench, 6);
        if (Bonus(independent) != 0) return false;
        var other = Fixture();
        var otherTarget = Add(other, new BaaSheepCardDefinition(), 1, BoardZone.Battlefield, 0);
        var loose = Add(other, new LockpickCardDefinition(), 2, null, 0);
        if (new CardEconomyService(other.Factory).SellCard(other.Session, loose.Id).IsFailure || Bonus(otherTarget) != 5) return false;
        foreach (var invalid in new[] { 0, -1 })
        {
            try { _ = new IncreaseLeftmostHasteCardOnSellDefinition(invalid); return false; }
            catch (ArgumentOutOfRangeException) { }
        }
        return true;

        bool FailedSalePreserves() => f.Economy.SellFromBoard(f.Session, overflow.Id).IsFailure
            && f.Session.Player.Wealth == wealth && f.Session.Random.State == random
            && f.Session.Player.Inventory.Find(overflow.Id) is not null && f.Session.Board.Contains(overflow.Id);
    }

    internal static bool ClassificationAndEcho()
    {
        var adjacent = Fixture();
        Add(adjacent, new NunCardDefinition(), 1, BoardZone.Battlefield, 0);
        var boots = Add(adjacent, new MilitaryBootsCardDefinition(), 1, BoardZone.Battlefield, 1);
        Add(adjacent, new BeastHideCardDefinition(), 1, BoardZone.Battlefield, 2);
        SellLock(adjacent, 2);
        if (Bonus(boots) != 5 || !HasteAmounts(Setup(adjacent)).SequenceEqual(new[] { 30, 15 })) return false;
        var overflow = Add(adjacent, new LockpickCardDefinition(), 2, null, 0);
        boots.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HasteDurationBonus, int.MaxValue / 2 - 19);
        var wealth = adjacent.Session.Player.Wealth;
        if (adjacent.Economy.SellCard(adjacent.Session, overflow.Id).IsSuccess
            || adjacent.Session.Player.Wealth != wealth || adjacent.Session.Player.Inventory.Find(overflow.Id) is null) return false;

        var echo = Fixture();
        Add(echo, new FlangedMaceCardDefinition(), 1, BoardZone.Battlefield, 0);
        var gear = Add(echo, new TransmissionGearCardDefinition(), 1, BoardZone.Battlefield, 1);
        var receiver = Add(echo, new BeastHideCardDefinition(), 1, BoardZone.Battlefield, 2);
        SellLock(echo, 2);
        var result = new CombatSimulator().Simulate(Setup(echo));
        if (Bonus(gear) != 5 || Bonus(receiver) != 0 || !HasteAmounts(Setup(echo)).SequenceEqual(new[] { 15 })
            || result.States.Last().Cards.Single(card => card.Id == receiver.Id).Haste != 15) return false;

        foreach (var enemy in new[] { false, true })
        {
            var f = Fixture();
            EffectDefinition effect = enemy
                ? new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.HasteDuration, 10, 2)
                : new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10);
            var source = Add(f, new HasteFixtureDefinition(effect), 1, BoardZone.Battlefield, 1);
            if (enemy)
                for (var index = 0; index < 2; index++) Add(f, new BeastHideCardDefinition(), 1, BoardZone.Battlefield, index, opponent: true);
            SellLock(f, 2);
            if (Bonus(source) != 5 || !HasteAmounts(Setup(f)).SequenceEqual(Enumerable.Repeat(15, enemy ? 2 : 1))) return false;
        }

        var quest = Fixture();
        var listener = Add(quest, new HasteFixtureDefinition(
            new ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition(BattleStatus.HasteDuration, GameTags.Human,
                GameAttributeKeys.AttackDamage, 1), passive: true), 1, BoardZone.Battlefield, 0);
        var questSource = Add(quest, new HasteFixtureDefinition(null), 1, BoardZone.Battlefield, 2);
        new CardQuestService(quest.Board).ProcessEvent(quest.Session, new CardAcquiredQuestEvent([GameElements.General]));
        SellLock(quest, 2);
        if (Bonus(listener) != 0 || Bonus(questSource) != 5 || !HasteAmounts(Setup(quest)).SequenceEqual(new[] { 15 })) return false;
        return true;
    }

    private static int Bonus(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HasteDurationBonus);
    private static int[] HasteAmounts(BattleSetup setup) => new CombatSimulator().Simulate(setup).Events
        .OfType<StatusChangedEvent>().Where(value => value.Status == BattleStatus.HasteDuration).Select(value => value.Amount).ToArray();
    private static BattleSetup Setup(FixtureData f) => new BattleSetupFactory().Create(f.Session, f.Opponent, 1, new BattleTick(50), new BattleTick(300));
    private static void SellLock(FixtureData f, int level)
    {
        var card = Add(f, new LockpickCardDefinition(), level, null, 0);
        if (f.Economy.SellCard(f.Session, card.Id).IsFailure) throw new InvalidOperationException("开锁器验证出售失败。");
    }
    private static CardInstance Add(FixtureData f, CardDefinition definition, int level, BoardZone? zone, int start, bool opponent = false)
    {
        var card = f.Factory.CreateCard(definition, level); var session = opponent ? f.Opponent : f.Session;
        session.Player.Inventory.Add(card);
        if (zone is { } location && f.Board.PlaceCard(session, card.Id, location, start).IsFailure)
            throw new InvalidOperationException("开锁器验证放置失败。");
        return card;
    }
    private static FixtureData Fixture()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var matches = new CreateMatchService(factory);
        var session = matches.Create(1, 100, new ValosHeroDefinition());
        var opponent = matches.Create(2, 0, new ValosHeroDefinition());
        session.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        opponent.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        return new FixtureData(factory, board, new CardEconomyService(factory, board), session, opponent);
    }
    private sealed record FixtureData(EntityFactory Factory, BoardService Board, CardEconomyService Economy,
        MatchSession Session, MatchSession Opponent);

    // 非正式内容：分别覆盖随机疾速、纯状态监听和任务赋予的疾速能力。
    private sealed class HasteFixtureDefinition : CardDefinition
    {
        internal HasteFixtureDefinition(EffectDefinition? effect, bool passive = false)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(new StringName("verification.haste_source"),
                "疾速验证", new StringName("valos"), CardSize.Small, [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 })),
                new TagSet(), effect is null ? Array.Empty<AbilityDefinition>()
                    : [new AbilityDefinition(new StringName("verification.haste"), passive ? AbilityActivation.PassiveAura : AbilityActivation.Active,
                        AbilityTarget.SelfCard, 0, passive ? 0 : 50, [effect])],
                quests: effect is null
                    ? [new CardQuestDefinition(new StringName("verification.haste_quest"),
                        new AcquiredElementCardQuestConditionDefinition(GameElements.General), 1,
                        [new AbilityDefinition(new StringName("verification.quest_haste"), AbilityActivation.PassiveOnBattleStart,
                            AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)])])]
                    : null)
        {
        }
    }
}
