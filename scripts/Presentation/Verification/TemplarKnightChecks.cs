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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 圣殿骑士分级攻击、相邻召唤、确定性及冻结回放验证（表现层验证模块）。
internal static class TemplarKnightChecks
{
    internal static bool LevelsAndEchoes()
    {
        var definition = new TemplarKnightCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.templar_knight") || identity.DisplayName != "圣殿骑士"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.Contains(GameTags.Human) || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || identity.DescriptionEntries[1].KeywordKey != CardKeywords.Summon
            || CardKeywords.DisplayName(CardKeywords.Summon) != "召唤") return false;
        for (var level = 3; level <= 4; level++)
        {
            var source = Knight(3, level);
            var attack = level == 3 ? 60 : 120;
            var swordAttack = level == 3 ? 20 : 40;
            var instance = new EntityFactory().CreateCard(definition, level);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != attack
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 40) return false;
            var setup = Battle([source], [], 80);
            var result = new CombatSimulator().Simulate(setup);
            var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
            if (summons.Length != 2 || summons.Any(item => item.Tick != BattleTick.Zero || item.Level != level
                    || item.CardKey != new StringName("card.riding_shortsword") || item.SourceCardId != source.EntityId)
                || !summons.Select(item => item.BoardStart).SequenceEqual(new[] { 2, 5 })
                || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var attacks = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == source.EntityId).ToArray();
            if (!attacks.Select(item => item.Tick.Value).SequenceEqual(new long[] { 40, 80 })
                || attacks.Any(item => item.RawDamage != attack)) return false;
            foreach (var summon in summons)
            {
                var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == summon.SummonedCardId).ToArray();
                var state = result.States[^1].Cards.Single(item => item.Id == summon.SummonedCardId);
                if (hits.Length != 2 || hits.Any(item => item.RawDamage != swordAttack)
                    || state.Level != level || state.CooldownUnits.Count != 0
                    || state.SummonedCard!.Identity.DisplayName != "骑佩短剑"
                    || state.Values[GameAttributeKeys.AttackDamage] != swordAttack
                    || !state.SummonedCard.Tags.Contains(GameTags.Equipment)
                    || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == summon.SummonedCardId && !item.IsEcho))
                    return false;
            }
        }
        return true;
    }

    internal static bool PositionsAndOrdering()
    {
        if (!Starts(Battle([Knight(0)], [], 1), 2) || !Starts(Battle([Knight(8)], [], 1), 7)) return false;
        var source = Knight(3);
        var left = Block(1, 2);
        var right = Block(5, 2);
        if (!Starts(Battle([source, left], [], 1), 5) || !Starts(Battle([source, right], [], 1), 2)
            || !Starts(Battle([source, left, right], [], 1))
            || !Starts(Battle([source, left with { IsOnBench = true }], [right], 1), 2, 5)
            || !Starts(Battle([source with { IsOnBench = true }], [], 1))) return false;
        var narrow = Battle([source], [], 1, capacity: 5);
        if (!Starts(narrow, 2)) return false;
        var competing = Battle([Knight(1), Knight(4)], [], 40);
        if (!Starts(competing, 0, 3, 6)) return false;
        var result = new CombatSimulator().Simulate(competing);
        if (result.Events.OfType<DamageDealtEvent>().Count(item => item.SourceCardId != competing.Player.Cards[0].EntityId
            && item.SourceCardId != competing.Player.Cards[1].EntityId) != 4) return false;
        // 同一通用效果可召唤中型卡，检查完整占格与新卡的初始冷却。
        var reusable = source with { Abilities = [Summon(new WindBladeCardDefinition())] };
        result = new CombatSimulator().Simulate(Battle([reusable], [], 70));
        if (!result.Events.OfType<CardSummonedEvent>().Select(item => item.BoardStart).SequenceEqual(new[] { 1, 5 })
            || result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId != reusable.EntityId && !item.IsEcho)
                .Any(item => item.Tick.Value != 70)
            || result.Events.OfType<DamageDealtEvent>().Count(item => item.RawDamage == 20) != 2) return false;
        return Starts(Battle([reusable, Block(0, 2)], [], 1), 5);
    }

    internal static bool LifecycleAndValidation()
    {
        var source = Knight(3);
        // 来源本场摧毁后召唤物仍然存在，并响应另一张相邻人类攻击卡。
        var destroy = new AbilityDefinition(new StringName("verification.templar.destroy"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 10, [new DestroyCardEffectDefinition(false)]);
        var ally = new CardBattleSetup(EntityId.New(), 1, 1, 40, Tags: new TagSet([GameTags.Human]));
        var result = new CombatSimulator().Simulate(Battle([source with { Abilities = source.Abilities!.Append(destroy).ToArray() }, ally], [], 40));
        var sword = result.Events.OfType<CardSummonedEvent>().Single(item => item.BoardStart == 2);
        if (result.States[^1].Cards.Single(item => item.Id == source.EntityId).Destroyed != true
            || result.States[^1].Cards.Single(item => item.Id == sword.SummonedCardId).Destroyed
            || result.Events.OfType<DamageDealtEvent>().Count(item => item.SourceCardId == sword.SummonedCardId && item.Tick.Value == 40) != 1) return false;
        // 永久摧毁召唤物也不能向对局发布不存在的库存变更。
        var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 10,
            [new AbilityDefinition(new StringName("verification.templar.destroy_summon"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 10,
                [new DestroyRandomEnemyCardEffectDefinition([GameTags.Equipment], [CardSize.Small], Permanent: true)])],
            UseLegacyAttack: false);
        result = new CombatSimulator().Simulate(Battle([source], [enemy], 20));
        if (result.States[^1].Cards.Count(item => item.SummonedCard is not null && item.Destroyed) != 2
            || result.PermanentChanges.Any()) return false;
        try
        {
            _ = DefinitionRegistry.Create([new PaladinHeroDefinition(), new TemplarKnightCardDefinition()]);
            return false;
        }
        catch (DefinitionValidationException) { }
        var registry = DefinitionRegistry.Create([new PaladinHeroDefinition(), new TemplarKnightCardDefinition(), new RidingShortswordCardDefinition()]);
        return registry.Cards.ContainsKey(new StringName("card.templar_knight"));
    }

    internal static bool PlaybackAndRestoration()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(1, 100, new PaladinHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(2, 100, new PaladinHeroDefinition());
        var knight = factory.CreateCard(new TemplarKnightCardDefinition(), 4);
        var enemy = factory.CreateCard(new TemplarKnightCardDefinition(), 3);
        player.Player.Inventory.Add(knight); opponent.Player.Inventory.Add(enemy);
        var board = VerificationFixtures.CreateBoardService();
        if (board.PlaceCard(player, knight.Id, BoardZone.Battlefield, 3).IsFailure
            || board.PlaceCard(opponent, enemy.Id, BoardZone.Battlefield, 3).IsFailure) return false;
        var before = MatchSnapshot.From(player);
        var enemyBefore = MatchSnapshot.From(opponent);
        var setup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(80), new BattleTick(81));
        var result = new CombatSimulator().Simulate(setup);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, result, before));
        var initial = playback.Project(SideId.Player);
        playback.Advance(0);
        var summoned = playback.Project(SideId.Player);
        var enemySummoned = playback.Project(SideId.Opponent);
        if (initial.Cards.Count != 1 || initial.BoardPlacements.Count != 1 || summoned.Cards.Count != 3
            || summoned.BattlefieldCount != 3 || summoned.BoardPlacements.Count != 3
            || summoned.Cards.Count(card => card.DisplayName == "骑佩短剑" && card.Level == 4 && card.CurrentValues[GameAttributeKeys.AttackDamage] == 40) != 2
            || enemySummoned.Cards.Count(card => card.DisplayName == "骑佩短剑" && card.Level == 3) != 2
            || !summoned.BoardPlacements.OrderBy(item => item.Start).Select(item => item.Start).SequenceEqual(new[] { 2, 3, 5 })) return false;
        playback.TogglePause();
        if (playback.Advance(8) || playback.Project(SideId.Player).Cards.Count != 3) return false;
        playback.TogglePause(); playback.ToggleSpeed(); playback.Advance(2);
        var frozen = playback.Project(SideId.Player);
        playback.Skip();
        var final = playback.Project(SideId.Player);
        var nextSetup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(1));
        var next = new CombatSimulator().Simulate(nextSetup);
        return final.Cards.Count == 3 && frozen.Cards.Count == 3 && summoned.Cards.Count == 3 && initial.Cards.Count == 1
            && MatchSnapshot.From(player).Cards.Count == 1 && MatchSnapshot.From(opponent).Cards.Count == 1
            && nextSetup.Player.Cards.Count == 1 && next.States[0].Cards.Count == 2
            && next.Events.OfType<CardSummonedEvent>().Count() == 4 && !result.PermanentChanges.Any();
    }

    private static AbilityDefinition Summon(CardDefinition template) =>
        new(new StringName("verification.templar.summon"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SummonAdjacentCardEffectDefinition(template, AdjacentCardSide.Left), new SummonAdjacentCardEffectDefinition(template, AdjacentCardSide.Right)]);
    private static CardBattleSetup Knight(int start, int level = 3)
    {
        var definition = new TemplarKnightCardDefinition();
        var configuration = definition.GetLevel(level)!;
        return new CardBattleSetup(EntityId.New(), start, configuration.BaseCombatValues[GameAttributeKeys.AttackDamage],
            40, configuration.Abilities, UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 2,
            ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Block(int start, int size) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false, OccupiedSlots: size);
    private static bool Starts(BattleSetup setup, params int[] starts) => new CombatSimulator().Simulate(setup).Events
        .OfType<CardSummonedEvent>().Select(item => item.BoardStart).SequenceEqual(starts);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int timeout, int capacity = 10) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), player) { BattlefieldCapacity = capacity },
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemy) { BattlefieldCapacity = capacity },
            42, new BattleTick(timeout), new BattleTick(timeout + 1));
}
