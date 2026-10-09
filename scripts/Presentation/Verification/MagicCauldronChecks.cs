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
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 魔法坩埚出售植物成长、任务阈值、升级与交易隔离验证（表现层验证模块）。
internal static class MagicCauldronChecks
{
    internal static bool LevelsAndGrowth()
    {
        var definition = new MagicCauldronCardDefinition();
        if (definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("mona")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 1 || !definition.Tags.Contains(GameTags.Medium)) return false;
        foreach (var level in Enumerable.Range(1, 4))
        {
            var f = Fixture(level);
            var bench = f.Factory.CreateCard(definition, level);
            var loose = f.Factory.CreateCard(definition, level);
            f.Session.Player.Inventory.Add(bench); f.Session.Player.Inventory.Add(loose);
            _ = f.Board.PlaceCard(f.Session, bench.Id, BoardZone.Bench, 0);
            var frozen = Setup(f, 140);
            SellPlant(f, BoardZone.Battlefield); SellPlant(f, BoardZone.Bench); SellPlant(f, null);
            var other = f.Factory.CreateCard(new SmallManaPotionCardDefinition());
            f.Session.Player.Inventory.Add(other);
            if (f.Economy.SellCard(f.Session, other.Id).IsFailure || Poison(f.Card) != 3 + 3 * level
                || Poison(bench) != 3 + 3 * level || Poison(loose) != 3
                || Progress(f.Card) != 3 || Progress(bench) != 3 || Progress(loose) != 0) return false;
            var original = new CombatSimulator().Simulate(frozen);
            if (original.Events.OfType<StatusChangedEvent>().Any(value => value.Amount != 3)) return false;
            var setup = Setup(f, 140);
            var result = new CombatSimulator().Simulate(setup);
            var statuses = result.Events.OfType<StatusChangedEvent>().Where(value => value.Status == BattleStatus.Poison).ToArray();
            if (statuses.Length != 2 || statuses.Any(value => value.Amount != 3 + 3 * level)
                || !statuses.Select(value => value.Tick.Value).SequenceEqual(new long[] { 70, 140 })
                || result.States.Last().Player.Poison != 0 || result.States.Any(value => value.Opponent.Armor != 500)
                || result.Events.OfType<AbilityActivatedEvent>().Any(value => value.SourceCardId == bench.Id)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var face = CardDisplayAdapter.FaceEffects(MatchSnapshot.From(f.Session).Cards.Single(value => value.Id == f.Card.Id));
            if (face.Single(value => value.Kind == CardFaceEffectKind.Poison).Value != (3 + 3 * level).ToString()) return false;
        }
        return true;
    }

    internal static bool ThresholdAndMerge()
    {
        var f = Fixture(1);
        for (var sale = 0; sale < 19; sale++) SellPlant(f, null);
        var frozen = Setup(f, 140);
        if (Progress(f.Card) != 19 || Cooldown(f.Card) != 70 || Poison(f.Card) != 22) return false;
        // 真正的同级获得合并保留原实例进度和成长，采用新等级的出售增量。
        var merged = f.Economy.AcquireCard(f.Session, new MagicCauldronCardDefinition(), 1, CardAcquisitionSource.Reward).Value!;
        if (merged.Id != f.Card.Id || merged.CurrentLevel != 2 || Progress(f.Card) != 19 || Poison(f.Card) != 22) return false;
        SellPlant(f, null);
        if (Progress(f.Card) != 20 || Cooldown(f.Card) != 50 || Poison(f.Card) != 24) return false;
        SellPlant(f, null); SellPlant(f, null);
        if (Progress(f.Card) != 20 || Cooldown(f.Card) != 50 || Poison(f.Card) != 28) return false;
        var setup = Setup(f, 100);
        var result = new CombatSimulator().Simulate(setup);
        if (!result.Events.OfType<StatusChangedEvent>().Select(value => value.Tick.Value).SequenceEqual(new long[] { 50, 100 })
            || result.Events.OfType<StatusChangedEvent>().Any(value => value.Amount != 28)
            || !new CombatSimulator().Simulate(frozen).Events.OfType<StatusChangedEvent>()
                .Select(value => value.Tick.Value).SequenceEqual(new long[] { 70, 140 })) return false;
        _ = f.Board.PlaceCard(f.Session, f.Card.Id, BoardZone.Bench, 0);
        if (Cooldown(f.Card) != 70 || Progress(f.Card) != 20) return false;
        SellPlant(f, null);
        if (Poison(f.Card) != 30 || Progress(f.Card) != 20) return false;
        _ = f.Board.PlaceCard(f.Session, f.Card.Id, BoardZone.Battlefield, 0);
        if (Cooldown(f.Card) != 50) return false;
        // 新实例不补计历史出售；在备战区完成任务后，入场时应用缩减。
        var fresh = f.Factory.CreateCard(new MagicCauldronCardDefinition(), 4);
        f.Session.Player.Inventory.Add(fresh);
        _ = f.Board.PlaceCard(f.Session, fresh.Id, BoardZone.Bench, 0);
        for (var sale = 0; sale < 20; sale++) SellPlant(f, null);
        if (Progress(fresh) != 20 || Poison(fresh) != 83 || Cooldown(fresh) != 70) return false;
        _ = f.Board.PlaceCard(f.Session, fresh.Id, BoardZone.Battlefield, 2);
        var snapshot = MatchSnapshot.From(f.Session).Cards.Single(value => value.Id == fresh.Id);
        return Cooldown(fresh) == 50 && snapshot.Quests.Single().Unlocked
            && snapshot.Quests.Single().Progress == 20
            && snapshot.Abilities.Single(ability => ability.Activation == AbilityActivation.Active).CooldownTicks == 50;
    }

    internal static bool FailedSalesAndIsolation()
    {
        var f = Fixture(1);
        for (var sale = 0; sale < 19; sale++) SellPlant(f, null);
        var plant = f.Factory.CreateCard(new BerriesCardDefinition());
        f.Session.Player.Inventory.Add(plant);
        var wealth = f.Session.Player.Wealth;
        var heroHealth = f.Session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var random = f.Session.Random.State;
        f.Card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Poison, int.MaxValue - 19);
        if (f.Economy.SellCard(f.Session, plant.Id).IsSuccess || Progress(f.Card) != 19 || Cooldown(f.Card) != 70
            || f.Session.Player.Inventory.Find(plant.Id) is null || f.Session.Player.Wealth != wealth
            || f.Session.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != heroHealth
            || f.Session.Random.State != random) return false;
        f.Card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Poison, 3);
        if (f.Economy.SellCard(f.Session, plant.Id).IsFailure || Progress(f.Card) != 20 || Poison(f.Card) != 23
            || f.Economy.SellCard(f.Session, plant.Id).IsSuccess || Poison(f.Card) != 23 || Progress(f.Card) != 20) return false;
        // 缺少棋盘依赖的游离出售入口也应在任务完成时刷新场上冷却。
        var other = Fixture(1);
        var withoutBoard = new CardEconomyService(other.Factory);
        for (var sale = 0; sale < 20; sale++)
        {
            var card = other.Factory.CreateCard(new BerriesCardDefinition());
            other.Session.Player.Inventory.Add(card);
            if (withoutBoard.SellCard(other.Session, card.Id).IsFailure) return false;
        }
        return Progress(other.Card) == 20 && Poison(other.Card) == 23 && Cooldown(other.Card) == 50
            && Progress(f.Card) == 20 && Poison(f.Card) == 23;
    }

    private static int Poison(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison);
    private static int Cooldown(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks);
    private static int Progress(CardInstance card) => card.GetQuestProgress(card.Quests.Single().Key);

    private static void SellPlant(FixtureData f, BoardZone? zone)
    {
        var plant = f.Factory.CreateCard(new BerriesCardDefinition());
        f.Session.Player.Inventory.Add(plant);
        if (zone is { } target) _ = f.Board.PlaceCard(f.Session, plant.Id, target, 4);
        var sold = zone is null ? f.Economy.SellCard(f.Session, plant.Id) : f.Economy.SellFromBoard(f.Session, plant.Id);
        if (sold.IsFailure) throw new System.InvalidOperationException(sold.Failure!.Message);
    }

    private static BattleSetup Setup(FixtureData f, int timeout) => new BattleSetupFactory().Create(
        f.Session, f.Opponent, 42, new BattleTick(timeout), new BattleTick(300));

    private static FixtureData Fixture(int level)
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board); var matches = new CreateMatchService(factory);
        var session = matches.Create(42, 100, new MonaHeroDefinition());
        var opponent = matches.Create(43, 100, new MonaHeroDefinition());
        opponent.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        opponent.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 500);
        var card = economy.AcquireAndPlace(session, new MagicCauldronCardDefinition(), level, CardAcquisitionSource.Reward).Value!.Card;
        return new FixtureData(factory, board, economy, session, opponent, card);
    }

    private sealed record FixtureData(EntityFactory Factory, BoardService Board, CardEconomyService Economy,
        MatchSession Session, MatchSession Opponent, CardInstance Card);
}
