using System;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 碧玉蟾价值施毒、参战成长、升级与冻结回放验证（表现层验证模块）。
internal static class JadeToadChecks
{
    internal static bool LevelsAndPoison()
    {
        var definition = new JadeToadCardDefinition();
        if (definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("jiyun")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Medium) || !definition.Tags.Contains(GameTags.Beast)) return false;
        foreach (var level in Enumerable.Range(1, 4))
        {
            var (session, opponent, card, board, economy) = Fixture(level);
            var currentValue = Value(card);
            card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 7));
            currentValue += 7;
            var setup = new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(100), new BattleTick(300));
            var before = MatchSnapshot.From(session).Cards.Single(value => value.Id == card.Id);
            var displayed = CardDisplayAdapter.FaceEffects(before).Single(value => value.Kind == CardFaceEffectKind.Poison);
            if (displayed.Value != currentValue.ToString()) return false;
            card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 100);
            var result = new CombatSimulator().Simulate(setup);
            var poison = result.Events.OfType<StatusChangedEvent>().Where(value => value.Status == BattleStatus.Poison).ToArray();
            if (poison.Length != 2 || poison.Any(value => value.Amount != currentValue)
                || !poison.Select(value => value.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                || result.States.Last().Opponent.Poison != currentValue * 2
                || result.States.Any(value => value.Opponent.Armor != 500)
                || result.States.Last().Player.Poison != 0 || Value(card) != 107
                || result.PermanentChanges.Count != 0
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        var zero = Fixture(1);
        zero.Card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 0);
        var zeroResult = new CombatSimulator().Simulate(new BattleSetupFactory().Create(
            zero.Session, zero.Opponent, 1, new BattleTick(50), new BattleTick(300)));
        return zeroResult.Events.OfType<StatusChangedEvent>().Single().Amount == 0
            && zeroResult.States.Last().Opponent.Poison == 0;
    }

    internal static bool GrowthAndParticipation()
    {
        var definition = new JadeToadCardDefinition();
        int[] bonuses = [3, 5, 8, 12];
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var outcome in new[] { BattleOutcome.PlayerVictory, BattleOutcome.OpponentVictory, BattleOutcome.Draw })
        foreach (var kind in new[] { MatchBattleKind.Pvp, MatchBattleKind.Monster })
        {
            var fixture = Fixture(level);
            var field = fixture.Card;
            var factory = new EntityFactory();
            var bench = factory.CreateCard(definition, level);
            bench.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 9);
            fixture.Session.Player.Inventory.Add(bench);
            _ = fixture.Board.PlaceCard(fixture.Session, bench.Id, BoardZone.Bench, 0);
            var initialValue = Value(field);
            var setup = new BattleSetupFactory().Create(fixture.Session, fixture.Opponent, 1, new BattleTick(1), new BattleTick(300));
            // 结算按开战位置，不按此刻位置；不要求已经发动。
            _ = fixture.Board.PlaceCard(fixture.Session, field.Id, BoardZone.Bench, 2);
            _ = fixture.Board.PlaceCard(fixture.Session, bench.Id, BoardZone.Battlefield, 0);
            var battle = WithOutcome(new CombatSimulator().Simulate(setup), outcome);
            if (new MatchResultService(fixture.Board).Apply(fixture.Session, battle, kind, opponent: fixture.Opponent).IsFailure
                || field.BattleValueBonus != bonuses[level - 1]
                || Value(field) != initialValue + bonuses[level - 1] || Value(bench) != 9) return false;
        }
        foreach (var permanent in new[] { false, true })
        {
            var fixture = Fixture(1);
            var original = new BattleSetupFactory().Create(fixture.Session, fixture.Opponent, 1, new BattleTick(1), new BattleTick(300));
            var source = original.Player.Cards.Single();
            var destroy = new AbilityDefinition("ability.verification.toad_destroy", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(permanent)]);
            var battle = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(original.Player.Hero,
                [source with { Abilities = [destroy] }]), original.Opponent, 1, new BattleTick(1), new BattleTick(300)));
            if (new MatchResultService(fixture.Board).Apply(fixture.Session, battle, MatchBattleKind.Pvp).IsFailure) return false;
            if (permanent ? fixture.Session.Player.Inventory.Find(fixture.Card.Id) is not null : Value(fixture.Card) != 5)
                return false;
        }
        return true;
    }

    internal static bool UpgradeSaleAndPlayback()
    {
        var fixture = Fixture(1);
        var service = new MatchResultService(fixture.Board);
        var setups = new BattleSetupFactory();
        var first = new CombatSimulator().Simulate(setups.Create(fixture.Session, fixture.Opponent, 1, new BattleTick(1), new BattleTick(300)));
        _ = service.Apply(fixture.Session, first, MatchBattleKind.Pvp);
        var definition = new JadeToadCardDefinition();
        var merged = fixture.Economy.AcquireCard(fixture.Session, definition, 1, CardAcquisitionSource.Reward).Value!;
        if (merged.Id != fixture.Card.Id || merged.CurrentLevel != 2 || Value(fixture.Card) != 5 || fixture.Card.BattleValueBonus != 5)
            return false;
        var before = MatchSnapshot.From(fixture.Session);
        var enemyBefore = MatchSnapshot.From(fixture.Opponent);
        var frozen = setups.Create(fixture.Session, fixture.Opponent, 1, new BattleTick(50), new BattleTick(300));
        var battle = new CombatSimulator().Simulate(frozen);
        if (battle.Events.OfType<StatusChangedEvent>().Single().Amount != 5) return false;
        _ = service.Apply(fixture.Session, battle, MatchBattleKind.Pvp);
        var after = MatchSnapshot.From(fixture.Session);
        if (Value(fixture.Card) != 10 || after.Cards.Single().Value != 10
            || CardDisplayAdapter.FaceEffects(after.Cards.Single()).Single(value => value.Kind == CardFaceEffectKind.Poison).Value != "10") return false;
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, battle, after));
        playback.TogglePause(); playback.ToggleSpeed(); playback.Skip();
        var replayCard = playback.Project(SideId.Player).Cards.Single();
        if (replayCard.Value != 5
            || CardDisplayAdapter.FaceEffects(replayCard).Single(value => value.Kind == CardFaceEffectKind.Poison).Value != "5"
            || Value(fixture.Card) != 10
            || new CombatSimulator().Simulate(frozen).Events.OfType<StatusChangedEvent>().Single().Amount != 5) return false;
        var next = new CombatSimulator().Simulate(setups.Create(fixture.Session, fixture.Opponent, 1, new BattleTick(50), new BattleTick(300)));
        if (next.Events.OfType<StatusChangedEvent>().Single().Amount != 10) return false;
        var wealth = fixture.Session.Player.Wealth;
        return fixture.Economy.SellFromBoard(fixture.Session, fixture.Card.Id).Value == 10
            && fixture.Session.Player.Wealth == wealth + 10 && fixture.Session.Player.Inventory.Find(fixture.Card.Id) is null;
    }

    private static int Value(CardInstance card) => card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);

    private static BattleResult WithOutcome(BattleResult battle, BattleOutcome outcome) => new(outcome, battle.EndReason,
        battle.EndedAt, battle.PlayerRemainingHealth, battle.OpponentRemainingHealth, battle.Events,
        battle.PermanentChanges, battle.States, battle.PermanentAttributeBonuses);

    private static (MatchSession Session, MatchSession Opponent, CardInstance Card, BoardService Board, CardEconomyService Economy) Fixture(int level)
    {
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var matches = new CreateMatchService(factory);
        var session = matches.Create(42, 100, new JiyunHeroDefinition());
        var opponent = matches.Create(43, 100, new JiyunHeroDefinition());
        opponent.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        opponent.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 500);
        var card = economy.AcquireCard(session, new JadeToadCardDefinition(), level, CardAcquisitionSource.Reward).Value!.Card;
        _ = board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        return (session, opponent, card, board, economy);
    }
}
