using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 冒险者日志的参战增值、出售经验事务与套装联动验证（表现层验证模块）。
internal static class AdventurerJournalChecks
{
    internal static bool Growth()
    {
        var definition = new AdventurerJournalCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.adventurer_journal") || identity.FactionKey != new StringName("robin")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || identity.SetKey != new StringName("set.adventurer") || definition.Tags.Count != 1
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)) return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var outcome in new[] { BattleOutcome.PlayerVictory, BattleOutcome.OpponentVictory, BattleOutcome.Draw })
        foreach (var kind in new[] { MatchBattleKind.Monster, MatchBattleKind.Pvp })
        {
            var (session, opponent, card, board, _) = Fixture(level);
            var bench = new EntityFactory().CreateCard(definition, level);
            session.Player.Inventory.Add(bench); board.PlaceCard(session, bench.Id, BoardZone.Bench, 0);
            var before = Value(card); var benchBefore = Value(bench);
            var setup = new BattleSetupFactory(DefinitionRegistry.Scan(typeof(AdventurerJournalChecks).Assembly).Sets)
                .Create(session, opponent, 42, new BattleTick(1));
            var frozen = new CombatSimulator().Simulate(setup);
            var battle = new BattleResult(outcome, frozen.EndReason, frozen.EndedAt, frozen.PlayerRemainingHealth,
                frozen.OpponentRemainingHealth, frozen.Events, frozen.PermanentChanges, frozen.States, frozen.PermanentAttributeBonuses);
            board.PlaceCard(session, card.Id, BoardZone.Bench, 2); board.PlaceCard(session, bench.Id, BoardZone.Battlefield, 0);
            if (new MatchResultService(board).Apply(session, battle, kind, opponent: opponent).IsFailure
                || card.BattleValueBonus != level * 2 || Value(card) != before + level * 2 || Value(bench) != benchBefore
                || card.Abilities.Count != 0 || card.OnSellReward is not GrantValuePercentExperienceOnSellDefinition { Percent: 10 }) return false;
        }
        return true;
    }

    internal static bool SaleAndFailures()
    {
        foreach (var value in new[] { 0, 9, 10, 19, 100, 297 })
        foreach (var zone in new BoardZone?[] { null, BoardZone.Battlefield, BoardZone.Bench })
        {
            var (session, _, card, board, economy) = Fixture(1);
            if (zone is null) board.RemoveFromBoard(session, card.Id);
            else board.PlaceCard(session, card.Id, zone.Value, 0);
            card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 0);
            card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, value));
            var experience = session.Player.Experience; var wealth = session.Player.Wealth;
            var hero = session.Player.Hero!;
            var heroLevel = hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
            var health = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
            var mana = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana);
            var gained = (experience + value / 10) / 10;
            var growth = ((heroLevel + 1 + heroLevel + gained) * gained * 10);
            var random = session.Random.State;
            if (economy.CheckSale(session, card.Id, zone is not null).IsFailure
                || session.Player.Experience != experience || session.Random.State != random || !session.Player.Inventory.Cards.Contains(card)) return false;
            var sold = zone is null ? economy.SellCard(session, card.Id) : economy.SellFromBoard(session, card.Id);
            if (sold.IsFailure || sold.Value != value || session.Player.Wealth != wealth + value
                || session.Player.Experience != (experience + value / 10) % 10
                || hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != heroLevel + gained
                || hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != health + growth
                || hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana) != mana + growth
                || economy.SellFromBoard(session, card.Id).IsSuccess || session.Random.State != random) return false;
        }
        foreach (var failure in new[] { "wealth", "growth", "hero" })
        {
            var (session, _, card, _, economy) = Fixture(1);
            card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 100);
            if (failure == "wealth") session.Player.AddWealth(int.MaxValue - session.Player.Wealth);
            if (failure == "growth") session.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, int.MaxValue);
            if (failure == "hero")
            {
                session = new MatchSession(42, 100); session.Player.Inventory.Add(card);
                new BoardService(new BoardPlacementSolver()).PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
            }
            var wealth = session.Player.Wealth; var experience = session.Player.Experience; var random = session.Random.State;
            if (economy.SellFromBoard(session, card.Id).IsSuccess || !session.Board.Contains(card.Id)
                || session.Player.Inventory.Find(card.Id) is null || session.Player.Wealth != wealth
                || session.Player.Experience != experience || session.Random.State != random) return false;
        }
        return true;
    }

    internal static bool SetAndUpgrade()
    {
        var (session, _, card, board, economy) = Fixture(1);
        var backpack = economy.AcquireAndPlace(session, new AdventurerBackpackCardDefinition(), 2, CardAcquisitionSource.Reward).Value!.Card;
        var greatsword = economy.AcquireAndPlace(session, new AdventurerGreatswordCardDefinition(), 3, CardAcquisitionSource.Reward).Value!.Card;
        var bow = economy.AcquireAndPlace(session, new AdventurerHuntingBowCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var registry = DefinitionRegistry.Scan(typeof(AdventurerJournalChecks).Assembly);
        var snapshot = MatchDisplayQuery.Capture(session, registry.Sets);
        if (snapshot.Sets.Single(set => set.Key == new StringName("set.adventurer")).DistinctCardCount != 4) return false;
        card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 99));
        var merged = economy.AcquireCard(session, new AdventurerJournalCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        if (merged.Id != card.Id || merged.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || merged.BattleValueBonus != 4 || Value(merged) < 100) return false;
        var old = MatchSnapshot.From(session); var value = Value(merged); var previousLevel = session.Player.Hero!.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        if (economy.SellFromBoard(session, merged.Id).IsFailure
            || session.Player.Hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != previousLevel + 1
            || old.Cards.Single(c => c.Id == card.Id).Value != value) return false;
        var after = MatchDisplayQuery.Capture(session, registry.Sets);
        var set = after.Sets.Single(set => set.Key == new StringName("set.adventurer"));
        return set.DistinctCardCount == 3 && set.Thresholds.Single(t => t.RequiredCount == 3).Active
            && !set.Thresholds.Single(t => t.RequiredCount == 5).Active;
    }

    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(AdventurerJournalChecks).Assembly);
        owner.GetViewport().UseHdr2D = false;
        var preview = new CardPreviewView { Theme = MatchTheme.Create(), Size = owner.GetViewportRect().Size };
        owner.AddChild(preview); preview.SetQuery(new CardPreviewQuery(registry));
        preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(new AdventurerJournalCardDefinition())), null, null);
        for (var frame = 0; frame < 10; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = owner.GetViewport().GetTexture().GetImage();
        if (image.SavePng("res://output/adventurer-journal/card-preview.png") != Error.Ok) throw new InvalidOperationException("日志截图保存失败。");
        owner.RemoveChild(preview); preview.Free();
    }

    private static int Value(CardInstance card) => card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);

    private static (MatchSession Session, MatchSession Opponent, CardInstance Card, BoardService Board, CardEconomyService Economy) Fixture(int level)
    {
        var factory = new EntityFactory();
        var registry = DefinitionRegistry.Scan(typeof(AdventurerJournalChecks).Assembly);
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board);
        var matches = new CreateMatchService(factory);
        var session = matches.Create(42, 100, new RobinHeroDefinition());
        var opponent = matches.Create(43, 100, new RobinHeroDefinition());
        var card = economy.AcquireAndPlace(session, new AdventurerJournalCardDefinition(), level, CardAcquisitionSource.Reward).Value!.Card;
        return (session, opponent, card, board, economy);
    }
}
