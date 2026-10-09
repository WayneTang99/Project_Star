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
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 银针草分级出售、治疗目标、冻结与交易失败验证（表现层）。
internal static class SilverNeedleGrassChecks
{
    internal static bool LevelsAndHealing()
    {
        var definition = new SilverNeedleGrassCardDefinition();
        var registry = DefinitionRegistry.Scan(typeof(MonaHeroDefinition).Assembly);
        var texture = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(definition.Attributes.Identity.Key) || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Attributes.Identity.DisplayName != "银针草" || definition.Attributes.Identity.FactionKey != new StringName("mona")
            || definition.Attributes.Identity.Size != CardSize.Small || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.Wood })
            || definition.Tags.Count != 3 || !definition.Tags.Contains(GameTags.Plant) || !definition.Tags.Contains(GameTags.Material)
            || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new MonaHeroDefinition());
            var opponent = matches.Create(2, 0, new MonaHeroDefinition());
            var nonHealer = Add(new BeastHideCardDefinition(), 1, BoardZone.Battlefield, 0);
            var right = Add(new NunCardDefinition(), 1, BoardZone.Battlefield, 6);
            var left = Add(new ArmyPriestCardDefinition(), 2, BoardZone.Battlefield, 2);
            var bench = Add(new NunCardDefinition(), 1, BoardZone.Bench, 0);
            var grass = Add(definition, level, zone, 8);
            var frozen = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(71));
            var wealth = session.Player.Wealth; var value = grass.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = session.Random.State;
            if (grass.Abilities.Count != 0 || grass.OnSellReward is not IncreaseLeftmostHealingCardOnSellDefinition { Amount: var amount }
                || amount != level * 5) return false;
            var sale = zone is null ? economy.SellCard(session, grass.Id) : economy.SellFromBoard(session, grass.Id);
            var next = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(71));
            var snapshot = MatchSnapshot.From(session).Cards.Single(card => card.Id == left.Id);
            if (sale.IsFailure || session.Player.Wealth != wealth + value || session.Random.State != random
                || session.Player.Inventory.Find(grass.Id) is not null || session.Board.Contains(grass.Id)
                || Bonus(left) != amount || Bonus(right) != 0 || Bonus(bench) != 0 || Bonus(nonHealer) != 0
                || economy.SellCard(session, grass.Id).IsSuccess || Bonus(left) != amount
                || Heal(frozen.Player.Cards.Single(card => card.EntityId == left.Id)) != 440
                || Heal(next.Player.Cards.Single(card => card.EntityId == left.Id)) != 440 + amount
                || !CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Healing
                    && effect.Value == (40 + amount).ToString())) return false;

            CardInstance Add(CardDefinition cardDefinition, int cardLevel, BoardZone? placed, int start)
            {
                var card = factory.CreateCard(cardDefinition, cardLevel); session.Player.Inventory.Add(card);
                if (placed is { } location && board.PlaceCard(session, card.Id, location, start).IsFailure)
                    throw new InvalidOperationException("银针草验证卡牌放置失败。");
                return card;
            }
        }
        return true;
    }

    internal static bool StackingAndFailures()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new CreateMatchService(factory).Create(1, 50, new MonaHeroDefinition());
        var healer = factory.CreateCard(new ArmyPriestCardDefinition()); session.Player.Inventory.Add(healer);
        board.PlaceCard(session, healer.Id, BoardZone.Battlefield, 2);
        var definition = new SilverNeedleGrassCardDefinition();
        var first = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        if (economy.SellCard(session, first.Id).IsFailure || Bonus(healer) != 5) return false;
        var second = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward);
        if (second.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || economy.SellCard(session, second.Id).IsFailure || Bonus(healer) != 15) return false;
        factory.ApplyCardLevel(healer, new ArmyPriestCardDefinition(), 3);
        if (Bonus(healer) != 15) return false;
        var overflow = economy.AcquireCard(session, definition, 4, CardAcquisitionSource.Reward).Value!.Card;
        board.PlaceCard(session, overflow.Id, BoardZone.Bench, 0);
        healer.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HealingBonus, int.MaxValue - 100);
        var wealth = session.Player.Wealth; var random = session.Random.State;
        if (economy.SellFromBoard(session, overflow.Id).IsSuccess || session.Player.Wealth != wealth || session.Random.State != random
            || session.Player.Inventory.Find(overflow.Id) is null || !session.Board.Contains(overflow.Id)
            || Bonus(healer) != int.MaxValue - 85) return false;
        healer.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HealingBonus, int.MaxValue - 20);
        if (economy.SellFromBoard(session, overflow.Id).IsSuccess || session.Player.Wealth != wealth
            || session.Player.Inventory.Find(overflow.Id) is null || !session.Board.Contains(overflow.Id)) return false;
        healer.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.HealingBonus, int.MaxValue - 115);
        if (economy.SellFromBoard(session, overflow.Id).IsFailure || Bonus(healer) != int.MaxValue - 80) return false;
        var opponent = new CreateMatchService(factory).Create(2, 0, new MonaHeroDefinition());
        var cappedSetup = new BattleSetupFactory().Create(session, opponent, 1, new BattleTick(71));
        if (Heal(cappedSetup.Player.Cards.Single(card => card.EntityId == healer.Id)) != 500) return false;
        var emptyTarget = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward).Value!.Card;
        board.PlaceCard(session, healer.Id, BoardZone.Bench, 2);
        if (economy.SellCard(session, emptyTarget.Id).IsFailure || Bonus(healer) != int.MaxValue - 80) return false;
        foreach (var amount in new[] { 0, -1 })
        {
            try { _ = new IncreaseLeftmostHealingCardOnSellDefinition(amount); return false; }
            catch (ArgumentOutOfRangeException) { }
        }
        return true;
    }

    private static int Bonus(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealingBonus);

    private static int Heal(CardBattleSetup healer)
    {
        var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 0,
            [new AbilityDefinition(new StringName("verification.opening_damage"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(100)])], UseLegacyAttack: false);
        var result = new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 500, 0, InitialMana: 100), [healer]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 500, 0), [enemy]), 1, new BattleTick(71)));
        return result.States.Last(frame => frame.Player.Health > 0).Player.Health;
    }
}
