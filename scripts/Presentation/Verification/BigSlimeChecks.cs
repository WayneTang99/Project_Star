using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 大史莱姆伤害、半血阈值、原位分裂和战斗隔离验证（表现层）。
internal static class BigSlimeChecks
{
    internal static bool LevelsAndSplit()
    {
        var definition = new BigSlimeCardDefinition(); var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(BigSlimeChecks).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString()); using var image = texture?.GetImage();
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "大史莱姆"
            || identity.FactionKey != GameFactions.Neutral || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Material) || !definition.Tags.Contains(GameTags.Medium)
            || image is null || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) return Fail();
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            var source = new CardBattleSetup(card.Id, 0, values[GameAttributeKeys.AttackDamage], 60, card.Abilities,
                UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2, ElementKeys: identity.ElementKeys)
                { Identity = identity, Level = level, CombatValues = values };
            var damage = 40 * (1 << (level - 1));
            var idle = new CombatSimulator().Simulate(Battle(source, reverse, 0, 130));
            if (!idle.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == card.Id)
                .Select(e => (e.Tick.Value, e.RawDamage)).SequenceEqual(new[] { (60L, damage), (120L, damage) })) return Fail();
            var seenElements = new System.Collections.Generic.HashSet<StringName>();
            foreach (var hit in new[] { 5000, 5001 })
            foreach (var seed in Enumerable.Range(1, 64).Select(value => (ulong)value))
            {
                // 原发动的多重已排到62/64 Tick，61 Tick分裂必须取消它们。
                var setup = Battle(source with { Multicast = 2 }, reverse, hit, 100, seed);
                var result = new CombatSimulator().Simulate(setup);
                var transformed = result.Events.OfType<CardTransformedEvent>().ToArray();
                var summoned = result.Events.OfType<CardSummonedEvent>().ToArray();
                if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
                if (hit == 5000)
                {
                    if (transformed.Length != 0 || summoned.Length != 0) return Fail();
                    continue;
                }
                if (transformed.Length != 1 || summoned.Length != 1 || transformed[0].Tick.Value != 61
                    || summoned[0].Tick.Value != 61 || summoned[0].BoardStart != 1 || summoned[0].Level != level
                    || result.Events.OfType<CardDestroyedEvent>().Any()) return Fail();
                var side = reverse ? SideId.Opponent : SideId.Player;
                var after = result.States.Last(s => s.Tick.Value == 61).Cards.Where(c => c.Side == side).ToArray();
                if (after.Length != 2 || after[0].TransformedIdentity?.OccupiedSlots != 1
                    || after[1].SummonedCard?.BoardStart != 1 || after[1].SummonedCard?.Identity.OccupiedSlots != 1
                    || after.Any(c => c.Level != level || c.Destroyed)) return Fail();
                var keys = new[] { transformed[0].ReplacementKey, summoned[0].CardKey };
                if (keys.Distinct().Count() != 2 || keys.Any(key => key == identity.Key || key == new StringName("card.slime"))) return Fail();
                foreach (var key in keys)
                {
                    var replacement = registry.Cards[key];
                    if (!replacement.SupportsLevel(level) || replacement.Attributes.Identity.Size != CardSize.Small) return Fail();
                    seenElements.Add(replacement.Attributes.Identity.ElementKeys.Single());
                }
                var fired = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceSide == side && !e.IsEcho).ToArray();
                if (fired.Count(e => e.Tick.Value == 60) != 1 || fired.Count(e => e.Tick.Value == 91) != 2
                    || fired.Any(e => e.Tick.Value is > 60 and < 91)) return Fail();
                if (card.Attributes.Identity.Key != identity.Key || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage
                    || !result.States[0].Cards.Any(c => c.Id == card.Id && c.TransformedIdentity is null && c.SummonedCard is null)) return Fail();
            }
            if (seenElements.Count != (level < 3 ? 7 : 9)) return Fail();
            var bench = new CombatSimulator().Simulate(Battle(source with { IsOnBench = true }, reverse, 5001, 100));
            if (bench.Events.OfType<CardTransformedEvent>().Any() || bench.Events.OfType<CardSummonedEvent>().Any()) return Fail();
        }
        return PlaybackAndRestoration();
    }

    private static bool PlaybackAndRestoration()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(1, 100, new PaladinHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(2, 100, new MonaHeroDefinition());
        var big = factory.CreateCard(new BigSlimeCardDefinition(), 3);
        var neighbor = factory.CreateCard(new SlimeCardDefinition(), 3);
        player.Player.Inventory.Add(big); player.Player.Inventory.Add(neighbor);
        var board = VerificationFixtures.CreateBoardService();
        if (board.PlaceCard(player, big.Id, BoardZone.Battlefield, 3).IsFailure
            || board.PlaceCard(player, neighbor.Id, BoardZone.Battlefield, 5).IsFailure) return Fail();
        var before = MatchSnapshot.From(player);
        var values = big.Attributes.BaseCombat.SnapshotFinalValues();
        var source = new CardBattleSetup(big.Id, 3, values[GameAttributeKeys.AttackDamage], 60, big.Abilities,
            UseLegacyAttack: false, Tags: big.Tags, OccupiedSlots: 2, ElementKeys: big.Attributes.Identity.ElementKeys)
            { Identity = big.Attributes.Identity, Level = 3, CombatValues = values };
        var setup = Battle(source, false, 5001, 100);
        var result = new CombatSimulator().Simulate(setup);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, MatchSnapshot.From(opponent), result, before));
        playback.Advance(6.2);
        var after = playback.Project(SideId.Player);
        var first = after.BoardPlacements.SingleOrDefault(p => p.CardId == big.Id);
        var extra = result.Events.OfType<CardSummonedEvent>().Single();
        var second = after.BoardPlacements.SingleOrDefault(p => p.CardId == extra.SummonedCardId);
        if (first is null || first.Start != 3 || first.EndExclusive != 4
            || second is null || second.Start != 4 || second.EndExclusive != 5
            || after.BoardPlacements.Single(p => p.CardId == neighbor.Id).Start != 5
            || after.Cards.Single(c => c.Id == big.Id).Size != CardSize.Small || after.BattlefieldCount != 3
            || !after.Cards.Single(c => c.Id == big.Id).Tags.Contains(GameTags.Material)
            || !after.Cards.Single(c => c.Id == extra.SummonedCardId).Tags.Contains(GameTags.Material)
            || before.BoardPlacements.Single(p => p.CardId == big.Id).EndExclusive != 5
            || MatchSnapshot.From(player).Cards.Single(c => c.Id == big.Id).Key != new StringName("card.big_slime")) return Fail();
        playback.Skip();
        return MatchSnapshot.From(player).BoardPlacements.Single(p => p.CardId == big.Id).EndExclusive == 5;
    }

    private static BattleSetup Battle(CardBattleSetup source, bool reverse, int hit, int ticks, ulong seed = 42)
    {
        var enemy = new CardBattleSetup(EntityId.New(), 0, hit, 61,
            hit == 0 ? [] : [new AbilityDefinition(new StringName("ability.verification_damage"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 61, [new DamageEffectDefinition(hit)])], UseLegacyAttack: false);
        var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source]);
        var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [enemy]);
        return new BattleSetup(reverse ? enemies : allies, reverse ? allies : enemies, seed,
            new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        GD.PushError("BigSlimeChecks 验证失败，行 " + line);
        return false;
    }
}
