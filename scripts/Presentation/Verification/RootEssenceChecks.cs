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

namespace Project_Star.Presentation.Verification;

// 树根精粹分级出售、永久再生叠加与战斗恢复验证（表现层验证模块）。
internal static class RootEssenceChecks
{
    internal static bool SalesAndRegeneration()
    {
        var definition = new RootEssenceCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.root_essence") || identity.DisplayName != "树根精粹"
            || identity.FactionKey != GameFactions.Neutral || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Small)
            || !definition.Tags.Contains(GameTags.Material)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Sell) return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var factory = new EntityFactory();
            var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new PaladinHeroDefinition());
            var opponent = matches.Create(2, 50, new PaladinHeroDefinition());
            var card = economy.AcquireCard(session, definition, level, CardAcquisitionSource.Reward).Value!.Card;
            if (zone is { } placed && board.PlaceCard(session, card.Id, placed, 0).IsFailure) return false;
            var before = session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealthRegen);
            var wealth = session.Player.Wealth;
            var value = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = session.Random.State;
            var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(11));
            if (card.Abilities.Count != 0
                || card.OnSellReward is not IncreaseHeroCombatAttributeOnSellDefinition bonus
                || bonus.AttributeKey != GameAttributeKeys.HealthRegen || bonus.Amount != level) return false;
            var sold = zone is null ? economy.SellCard(session, card.Id) : economy.SellFromBoard(session, card.Id);
            var next = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(11));
            if (sold.IsFailure || sold.Value != value || session.Player.Wealth != wealth + value
                || session.Random.State != random || session.Player.Inventory.Find(card.Id) is not null
                || session.Board.Contains(card.Id) || frozen.Player.Hero.HealthRegen != before
                || next.Player.Hero.HealthRegen != before + level
                || economy.SellFromBoard(session, card.Id).IsSuccess) return false;
            // 真实模拟在1秒先扣中毒再恢复，读取出售后冻结的生命再生。
            var damaged = new BattleSetup(new BattleSideSetup(next.Player.Hero with { Poison = 10 }, next.Player.Cards),
                next.Opponent, next.Seed, next.Timeout, next.EclipseTime);
            var atOneSecond = new CombatSimulator().Simulate(damaged).States.Last(state => state.Tick.Value == 10);
            if (atOneSecond.Player.Health != next.Player.Hero.MaxHealth - 10 + before + level) return false;
            // 再次获得的两张1级卡合并为2级，出售增量更新且既有贡献保留。
            var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
            var merged = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!;
            if (merged.Id != first.Id || merged.CurrentLevel != 2 || economy.SellCard(session, first.Id).IsFailure
                || session.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealthRegen) != before + level + 2
                || session.Player.Hero.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.HealthRegen) != before) return false;
        }
        return true;
    }
}
