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
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 磨刀石出售的目标筛选、永久攻击与交易隔离验证（表现层）。
internal static class WhetstoneChecks
{
    internal static bool LevelsAndDamage()
    {
        var definition = new WhetstoneCardDefinition();
        var registry = DefinitionRegistry.Scan(typeof(PaladinHeroDefinition).Assembly);
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || identity.DisplayName != "磨刀石" || identity.FactionKey != new StringName("neutral")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Material)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Sell
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new PaladinHeroDefinition());
            var opponent = matches.Create(2, 0, new PaladinHeroDefinition());
            var support = Add(new NunCardDefinition(), 1, BoardZone.Battlefield, 0);
            var right = Add(new FlangedMaceCardDefinition(), 1, BoardZone.Battlefield, 6);
            var left = Add(new FlangedMaceCardDefinition(), 1, BoardZone.Battlefield, 3);
            var bench = Add(new FlangedMaceCardDefinition(), 1, BoardZone.Bench, 0);
            var stone = Add(definition, level, zone, 8);
            var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(51));
            var wealth = session.Player.Wealth; var value = stone.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = session.Random.State;
            if (stone.Abilities.Count != 0 || stone.OnSellReward is not IncreaseLeftmostAttackCardOnSellDefinition { Amount: var amount }
                || amount != level * 5) return false;
            var sale = zone is null ? economy.SellCard(session, stone.Id) : economy.SellFromBoard(session, stone.Id);
            var next = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(51));
            var oldResult = new CombatSimulator().Simulate(frozen);
            var newResult = new CombatSimulator().Simulate(next);
            var snapshot = MatchSnapshot.From(session).Cards.Single(card => card.Id == left.Id);
            if (sale.IsFailure || session.Player.Wealth != wealth + value || session.Random.State != random
                || session.Player.Inventory.Find(stone.Id) is not null || session.Board.Contains(stone.Id)
                || Attack(left) != 10 + amount || Attack(right) != 10 || Attack(bench) != 10 || Attack(support) != 0
                || economy.SellCard(session, stone.Id).IsSuccess || Attack(left) != 10 + amount
                || frozen.Player.Cards.Single(card => card.EntityId == left.Id).AttackDamage != 10
                || next.Player.Cards.Single(card => card.EntityId == left.Id).AttackDamage != 10 + amount
                || oldResult.States.Last(frame => frame.Opponent.Health > 0).Opponent.Health
                    - newResult.States.Last(frame => frame.Opponent.Health > 0).Opponent.Health != amount
                || !CardDisplayAdapter.Details(snapshot).Contains($"当前 {10 + amount}")) return false;

            CardInstance Add(CardDefinition cardDefinition, int cardLevel, BoardZone? placed, int start)
            {
                var card = factory.CreateCard(cardDefinition, cardLevel); session.Player.Inventory.Add(card);
                if (placed is { } location && board.PlaceCard(session, card.Id, location, start).IsFailure)
                    throw new InvalidOperationException("磨刀石验证卡牌放置失败。");
                return card;
            }
        }
        return true;
    }

    internal static bool StackingAndFailures()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new CreateMatchService(factory).Create(1, 50, new PaladinHeroDefinition());
        var target = factory.CreateCard(new FlangedMaceCardDefinition()); session.Player.Inventory.Add(target);
        board.PlaceCard(session, target.Id, BoardZone.Battlefield, 2);
        var definition = new WhetstoneCardDefinition();
        var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        if (economy.SellCard(session, first.Id).IsFailure || Attack(target) != 15) return false;
        var second = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward);
        if (second.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || economy.SellCard(session, second.Id).IsFailure || Attack(target) != 25) return false;
        factory.ApplyCardLevel(target, new FlangedMaceCardDefinition(), 2);
        if (Attack(target) != 35) return false;
        var overflow = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!.Card;
        board.PlaceCard(session, overflow.Id, BoardZone.Bench, 0);
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.AttackDamage, int.MaxValue - 15);
        var wealth = session.Player.Wealth; var random = session.Random.State;
        if (economy.CheckSale(session, overflow.Id).IsSuccess || economy.SellFromBoard(session, overflow.Id).IsSuccess
            || session.Player.Wealth != wealth || session.Random.State != random
            || session.Player.Inventory.Find(overflow.Id) is null || !session.Board.Contains(overflow.Id)
            || Attack(target) != int.MaxValue) return false;
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.AttackDamage, int.MaxValue - 35);
        if (economy.SellFromBoard(session, overflow.Id).IsFailure || Attack(target) != int.MaxValue) return false;
        board.PlaceCard(session, target.Id, BoardZone.Bench, 2);
        var empty = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        if (economy.SellCard(session, empty.Id).IsFailure || Attack(target) != int.MaxValue) return false;
        foreach (var amount in new[] { 0, -1 })
        {
            try { _ = new IncreaseLeftmostAttackCardOnSellDefinition(amount); return false; }
            catch (ArgumentOutOfRangeException) { }
        }
        return true;
    }

    internal static bool AttackClassification()
    {
        foreach (var effect in new EffectDefinition[]
        {
            new DamageEffectDefinition(0), new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
            new MaxHealthPercentDamageEffectDefinition(1), new SourceHeroLevelScaledDamageEffectDefinition(1),
            new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
            new SourceHeroArmorDamageEffectDefinition(),
        })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var session = new CreateMatchService(factory).Create(1, 50, new PaladinHeroDefinition());
            var target = factory.CreateCard(new AttackFixture(effect)); session.Player.Inventory.Add(target);
            board.PlaceCard(session, target.Id, BoardZone.Battlefield, 4);
            var stone = economy.AcquireCard(session, new WhetstoneCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            if (economy.SellCard(session, stone.Id).IsFailure || Attack(target) != 5) return false;
        }
        return true;
    }

    private static int Attack(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage);

    // 直接伤害分类的内部夹具，不加入正式内容池（表现层验证模块）。
    private sealed class AttackFixture : CardDefinition
    {
        internal AttackFixture(EffectDefinition effect) : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("verification.attack_target"), "攻击目标", new StringName("neutral"),
                CardSize.Small, [GameElements.General]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 0 })), new TagSet(),
            [new AbilityDefinition(new StringName("verification.attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 10, [effect])]) { }
    }
}
