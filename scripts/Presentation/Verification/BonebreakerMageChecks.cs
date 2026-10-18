using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 碎骨法师摧毁筛选、逐张伤害及召唤复用位置验证（表现层验证模块）。
internal static class BonebreakerMageChecks
{
    internal static bool LevelsAndSacrifice()
    {
        var definition = new BonebreakerMageCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.bonebreaker_mage") || identity.DisplayName != "碎骨法师"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark }) || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(BonebreakerMageChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var bonus in new[] { 0, 7 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var attack = (level == 3 ? 80 : 160) + bonus;
            var mage = Mage(level) with { AttackDamage = attack, Multicast = 1 };
            if (mage.Abilities!.Single().ManaCost != 40) return false;
            var one = Block(2, 1, GameElements.Dark);
            var two = Block(3, 1, GameElements.Dark) with { ElementKeys = [GameElements.Dark, GameElements.Light] };
            var medium = Block(4, 2, GameElements.Dark);
            var otherElement = Block(6, 1, GameElements.Fire);
            var alreadyDead = Block(7, 1, GameElements.Dark) with { Abilities = [new AbilityDefinition(
                new StringName("verification.sacrifice.dead"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard,
                0, 0, [new DestroyCardEffectDefinition(false)])] };
            var bench = Block(8, 1, GameElements.Dark) with { IsOnBench = true };
            var enemy = Block(0, 1, GameElements.Dark);
            CardBattleSetup[] allies = [mage, one, two, medium, otherElement, alreadyDead, bench];
            var setup = Battle(side == SideId.Player ? allies : [enemy], side == SideId.Player ? [enemy] : allies, 180);
            var result = new CombatSimulator().Simulate(setup);
            var destroyed = result.Events.OfType<CardDestroyedEvent>().Where(item => item.SourceCardId == mage.EntityId).ToArray();
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == mage.EntityId).ToArray();
            var payments = result.Events.OfType<ManaChangedEvent>().Where(item => item.Side == side).ToArray();
            if (destroyed.Length != 2 || !destroyed.Select(item => item.CardId).SequenceEqual(new[] { one.EntityId, two.EntityId })
                || hits.Length != 2 || hits.Any(item => item.Tick.Value != 90 || item.RawDamage != attack
                    || item.TargetSide == side) || hits.Sum(item => item.ArmorAbsorbed) != 80
                || hits.Sum(item => item.HealthDamage) != attack * 2 - 80
                || result.States[^1].Cards.Any(item => new[] { medium.EntityId, otherElement.EntityId, bench.EntityId, enemy.EntityId }
                    .Contains(item.Id) && item.Destroyed)
                || payments.Length != 2 || payments.Any(item => item.Amount != -40)
                || result.PermanentChanges.Any() || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(offer).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == (level == 3 ? "80" : "160"))
                || !new CardDisplayAdapter().Build(offer).Effects.Any(effect => effect.Kind == CardFaceEffectKind.ManaCost && effect.Value == "−40")
                || !CardDisplayAdapter.Details(offer).Contains("攻击　")) return false;
        }
        var empty = new CombatSimulator().Simulate(Battle([Mage(3)], [], 180));
        if (empty.Events.OfType<DamageDealtEvent>().Any() || empty.Events.OfType<CardDestroyedEvent>().Any()) return false;
        var unpaidMage = Mage(3); var sacrifice = Block(2, 1, GameElements.Dark);
        var unpaid = new CombatSimulator().Simulate(Battle([unpaidMage, sacrifice], [], 90, 39));
        if (unpaid.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == unpaidMage.EntityId)
            || unpaid.Events.OfType<CardDestroyedEvent>().Any() || unpaid.Events.OfType<DamageDealtEvent>().Any()) return false;
        var paid = new CombatSimulator().Simulate(Battle([unpaidMage, sacrifice], [], 90, 40));
        if (paid.Events.OfType<CardDestroyedEvent>().Count() != 1
            || paid.Events.OfType<ManaChangedEvent>().Single().Amount != -40) return false;
        try
        {
            _ = new AbilityDefinition(new StringName("verification.sacrifice.invalid"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 90,
                [new DestroyAlliedCardsAndDamageEffectDefinition(CardSize.Small, new StringName(""), GameAttributeKeys.AttackDamage)]);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    internal static bool RepeatedSummoningAndPlayback()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(42, 100, new MonaHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(43, 100, new MonaHeroDefinition());
        opponent.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        var mage = factory.CreateCard(new BonebreakerMageCardDefinition(), 3);
        var skeleton = factory.CreateCard(new SkeletonCardDefinition(), 3);
        var barrow = factory.CreateCard(new BoneBarrowCardDefinition(), 3);
        var board = VerificationFixtures.CreateBoardService();
        player.Player.Inventory.Add(mage); player.Player.Inventory.Add(skeleton); player.Player.Inventory.Add(barrow);
        if (board.PlaceCard(player, mage.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(player, skeleton.Id, BoardZone.Battlefield, 2).IsFailure
            || board.PlaceCard(player, barrow.Id, BoardZone.Battlefield, 3).IsFailure) return false;
        var before = MatchSnapshot.From(player);
        var enemyBefore = MatchSnapshot.From(opponent);
        var setup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(180), new BattleTick(181));
        var result = new CombatSimulator().Simulate(setup);
        var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
        if (summons.Length != 2 || !summons.Select(item => item.Tick.Value).SequenceEqual(new long[] { 90, 180 })
            || summons.Any(item => item.BoardStart != 2 || item.Level != 3)
            || summons[0].SummonedCardId == summons[1].SummonedCardId
            || result.Events.OfType<CardDestroyedEvent>().Count(item => item.SourceCardId == mage.Id) != 2
            || result.PermanentChanges.Any() || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, result, before));
        var initial = playback.Project(SideId.Player);
        playback.Advance(9);
        var first = playback.Project(SideId.Player);
        playback.Skip();
        var final = playback.Project(SideId.Player);
        if (initial.BoardPlacements.Single(item => item.Start == 2).CardId != skeleton.Id
            || first.BoardPlacements.Single(item => item.Start == 2).CardId != summons[0].SummonedCardId
            || final.BoardPlacements.Single(item => item.Start == 2).CardId != summons[1].SummonedCardId
            || final.BoardPlacements.Count != 3 || final.BattlefieldCount != 3 || first.BoardPlacements.Count != 3
            || before.Cards.Count != 3 || before.BoardPlacements.Count != 3
            || MatchSnapshot.From(player).Cards.Count != 3 || new CombatSimulator().Simulate(setup).Events.OfType<CardSummonedEvent>().Count() != 2)
            return false;
        return result.States[^1].Cards.Single(item => item.Id == skeleton.Id).Destroyed
            && result.States[^1].Cards.Single(item => item.Id == summons[0].SummonedCardId).Destroyed
            && !result.States[^1].Cards.Single(item => item.Id == summons[1].SummonedCardId).Destroyed;
    }

    private static CardBattleSetup Mage(int level)
    {
        var definition = new BonebreakerMageCardDefinition();
        return new CardBattleSetup(EntityId.New(), 0, definition.GetLevel(level)!.BaseCombatValues[GameAttributeKeys.AttackDamage],
            90, definition.GetLevel(level)!.Abilities, UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 2,
            ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Block(int start, int size, StringName element) =>
        new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false, OccupiedSlots: size, ElementKeys: [element]);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int timeout, int mana = 80) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 80, InitialMana: mana, ManaRegen: 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 80, InitialMana: mana, ManaRegen: 0), opponent),
            42, new BattleTick(timeout), new BattleTick(timeout + 1));
}
