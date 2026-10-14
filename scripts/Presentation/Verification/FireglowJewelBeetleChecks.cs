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

// 萤火吉丁虫等级、出售成长及三类灼伤施加验证（表现层）。
internal static class FireglowJewelBeetleChecks
{
    internal static bool LevelsAndSales()
    {
        var definition = new FireglowJewelBeetleCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!DefinitionRegistry.Scan(typeof(MonaHeroDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || identity.DisplayName != "萤火吉丁虫" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire })
            || definition.InitialLevel != 1 || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Insect) || texture is null || texture.GetHeight() != texture.GetWidth() * 2
            || !identity.DescriptionEntries.Select(entry => entry.KeywordKey).SequenceEqual(new[] { CardKeywords.Activate, CardKeywords.Sell })) return false;
        foreach (var (level, amount) in new[] { (1, 3), (2, 6), (3, 10), (4, 16) })
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench, (BoardZone?)null })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new MonaHeroDefinition());
            var enemy = matches.Create(2, 0, new MonaHeroDefinition());
            var target = Add(definition, level, BoardZone.Battlefield, 3);
            var right = Add(definition, level, BoardZone.Battlefield, 6);
            var support = Add(new BlackRhinocerosBeetleCardDefinition(), 1, BoardZone.Battlefield, 1);
            var bench = Add(definition, level, BoardZone.Bench, 3);
            var sold = Add(definition, level, zone, 0);
            var ability = sold.Abilities.Single();
            if (ability.CooldownTicks != 40 || ability.ManaCost != 0 || ability.Target != AbilityTarget.EnemyHero
                || sold.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != amount
                || sold.OnSellReward is not IncreaseLeftmostBurnCardOnSellDefinition { Amount: var increment } || increment != level) return false;
            var initial = new BattleSetupFactory().Create(session, enemy, 9, new BattleTick(40));
            var wealth = session.Player.Wealth; var value = sold.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var random = session.Random.State;
            if ((zone is null ? economy.SellCard(session, sold.Id) : economy.SellFromBoard(session, sold.Id)).IsFailure
                || session.Player.Wealth != wealth + value || session.Random.State != random
                || session.Player.Inventory.Find(sold.Id) is not null || session.Board.Contains(sold.Id)
                || Bonus(target) != level || Bonus(right) != 0 || Bonus(bench) != 0 || Bonus(support) != 0
                || economy.SellCard(session, sold.Id).IsSuccess || Bonus(target) != level) return false;
            var setup = new BattleSetupFactory().Create(session, enemy, 9, new BattleTick(40));
            var result = new CombatSimulator().Simulate(setup);
            var burns = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
            if (burns.Length != 2 || burns[0].Amount != amount + level || burns[1].Amount != amount
                || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == target.Id && item.Tick.Value != 40)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || new CombatSimulator().Simulate(initial).Events.OfType<StatusChangedEvent>()
                    .Where(item => item.Status == BattleStatus.Burn).Any(item => item.Amount != amount)
                || CardDisplayAdapter.FaceEffects(MatchSnapshot.From(session).Cards.Single(card => card.Id == target.Id))
                    .Single(effect => effect.Kind == CardFaceEffectKind.Burn).Value != (amount + level).ToString()) return false;

            CardInstance Add(CardDefinition content, int cardLevel, BoardZone? location, int start)
            {
                var card = factory.CreateCard(content, cardLevel); session.Player.Inventory.Add(card);
                if (location is { } placed && board.PlaceCard(session, card.Id, placed, start).IsFailure)
                    throw new InvalidOperationException("萤火吉丁虫验证放置失败。");
                return card;
            }
        }
        return true;
    }

    internal static bool ClassificationAndPercent()
    {
        foreach (var effect in new EffectDefinition[]
        {
            new ApplyStatusEffectDefinition(BattleStatus.Burn, 0),
            new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn),
            new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage, 10),
        })
        foreach (var berserk in new[] { false, true })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new MonaHeroDefinition());
            var target = factory.CreateCard(new BurnFixture(effect, berserk)); session.Player.Inventory.Add(target);
            if (berserk) target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Multicast, 1);
            board.PlaceCard(session, target.Id, BoardZone.Battlefield, 0);
            var sold = factory.CreateCard(new FireglowJewelBeetleCardDefinition(), 4); session.Player.Inventory.Add(sold);
            if (economy.SellCard(session, sold.Id).IsFailure || Bonus(target) != 4) return false;
            var setup = new BattleSetupFactory().Create(session, matches.Create(2, 0, new MonaHeroDefinition()), 3, new BattleTick(berserk ? 12 : 10));
            var basic = effect is ApplyStatusEffectDefinition ? 0 : 10;
            var expected = berserk ? (basic + 4) * 120 / 100 : basic + 4;
            var burns = new CombatSimulator().Simulate(setup).Events.OfType<StatusChangedEvent>()
                .Where(item => item.Status == BattleStatus.Burn).ToArray();
            if (burns.Length != (berserk ? 2 : 1) || burns.Any(item => item.Amount != expected)) return false;
            if (CardDisplayAdapter.FaceEffects(MatchSnapshot.From(session).Cards.Single())
                .Single(effect => effect.Kind == CardFaceEffectKind.Burn).Value != (basic + 4).ToString()) return false;
        }
        var dragon = new RadiantDragonCardDefinition();
        if (!dragon.GetLevel(3)!.Abilities.SelectMany(ability => ability.Effects).Any(BurnEffectRules.AppliesBurn)) return false;
        return !BurnEffectRules.AppliesBurn(new ApplyStatusEffectDefinition(BattleStatus.Poison, 10));
    }

    internal static bool StackingAndFailures()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new CreateMatchService(factory).Create(1, 50, new MonaHeroDefinition());
        var definition = new FireglowJewelBeetleCardDefinition();
        var target = factory.CreateCard(definition); session.Player.Inventory.Add(target);
        board.PlaceCard(session, target.Id, BoardZone.Battlefield, 0);
        var first = economy.AcquireCard(session, definition, 2, CardAcquisitionSource.Reward).Value!.Card;
        if (economy.SellCard(session, first.Id).IsFailure || Bonus(target) != 2) return false;
        var second = economy.AcquireCard(session, definition, 2, CardAcquisitionSource.Reward).Value!.Card;
        economy.AcquireCard(session, definition, 2, CardAcquisitionSource.Reward);
        if (second.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
            || economy.SellCard(session, second.Id).IsFailure || Bonus(target) != 5) return false;
        factory.ApplyCardLevel(target, definition, 4);
        if (Bonus(target) != 5 || target.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 16) return false;
        var sold = factory.CreateCard(definition, 4); session.Player.Inventory.Add(sold);
        board.PlaceCard(session, sold.Id, BoardZone.Bench, 0);
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Burn, int.MaxValue - 8);
        var wealth = session.Player.Wealth;
        if (economy.CheckSale(session, sold.Id).IsSuccess || economy.SellFromBoard(session, sold.Id).IsSuccess
            || session.Player.Wealth != wealth || !session.Board.Contains(sold.Id) || Bonus(target) != 5) return false;
        target.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Burn, int.MaxValue - 9);
        if (economy.SellFromBoard(session, sold.Id).IsFailure || Bonus(target) != 9) return false;
        board.PlaceCard(session, target.Id, BoardZone.Bench, 3);
        var alone = factory.CreateCard(definition); session.Player.Inventory.Add(alone);
        board.PlaceCard(session, alone.Id, BoardZone.Battlefield, 0);
        if (economy.SellFromBoard(session, alone.Id).IsFailure || Bonus(target) != 9) return false;
        foreach (var amount in new[] { 0, -1 })
        {
            try { _ = new IncreaseLeftmostBurnCardOnSellDefinition(amount); return false; }
            catch (ArgumentOutOfRangeException) { }
        }
        return true;
    }

    private static int Bonus(CardInstance card) => card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.BurnBonus);

    // 三种灼伤量来源的内部目标夹具，不注册为正式卡牌（表现层验证）。
    private sealed class BurnFixture : CardDefinition
    {
        internal BurnFixture(EffectDefinition effect, bool berserk) : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("verification.burn_target"), "灼伤目标", new StringName("neutral"),
                CardSize.Small, [GameElements.Fire]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.Burn] = 10, [GameAttributeKeys.AttackDamage] = 109 })), new TagSet(),
            CreateAbilities(effect, berserk)) { }

        private static AbilityDefinition[] CreateAbilities(EffectDefinition effect, bool berserk)
        {
            var active = new AbilityDefinition(new StringName("verification.burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 10, [effect]);
            return berserk ? [active, new AbilityDefinition(new StringName("verification.berserk"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)])] : [active];
        }
    }
}
