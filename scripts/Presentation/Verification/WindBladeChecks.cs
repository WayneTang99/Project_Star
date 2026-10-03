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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 风之刃分级伤害、自身发动回响及多重成长生命周期验证（表现层验证模块）。
internal static class WindBladeChecks
{
    internal static bool LevelsAndEchoes()
    {
        var definition = new WindBladeCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "风之刃" || identity.Key != new StringName("card.wind_blade")
            || identity.FactionKey != new StringName("jiyun") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Wind })
            || !definition.Tags.Contains(GameTags.Equipment) || !definition.Tags.Contains(GameTags.Medium)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || identity.DescriptionEntries[1].KeywordKey != CardKeywords.Echo) return false;
        for (var level = 3; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var damage = level == 3 ? 20 : 40;
            var blade = Card(instance.Id, instance.Abilities) with { AttackDamage = damage };
            var bench = blade with { EntityId = EntityId.New(), IsOnBench = true };
            var enemy = blade with { EntityId = EntityId.New() };
            var ally = Card(EntityId.New(), [new AbilityDefinition(new StringName("verification.wind.ally"),
                AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 35, [new DamageEffectDefinition(1)])]);
            var setup = Battle([blade, ally, bench], [enemy], 280);
            var result = new CombatSimulator().Simulate(setup);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == instance.Id).ToArray();
            for (var batch = 1; batch <= 4; batch++)
                if (activations.Count(item => item.Tick.Value == batch * 70 && !item.IsEcho) != 1 << (batch - 1)
                    || activations.Count(item => item.Tick.Value == batch * 70 && item.IsEcho) != 1 << (batch - 1)) return false;
            var changes = result.Events.OfType<CardAttributeChangedEvent>().Where(item => item.CardId == instance.Id).ToArray();
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 70
                || instance.Abilities.Any(ability => ability.ManaCost != 0)
                || changes.Length != 15 || !changes.Select(item => item.CurrentValue).SequenceEqual(Enumerable.Range(1, 15))
                || changes.Any(item => item.AttributeKey != GameAttributeKeys.Multicast || item.Amount != 1)
                || result.Events.OfType<DamageDealtEvent>().Count(item => item.SourceCardId == instance.Id && item.RawDamage == damage) != 15
                || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == bench.EntityId)
                || result.States[^1].Cards.Single(item => item.Id == instance.Id).Values[GameAttributeKeys.Multicast] != 15
                || result.States[^1].Cards.Single(item => item.Id == enemy.EntityId).Values[GameAttributeKeys.Multicast] != 15
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        return true;
    }

    internal static bool PlaybackAndReset()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(1, 100, new JiyunHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(2, 100, new MonaHeroDefinition());
        var blade = factory.CreateCard(new WindBladeCardDefinition());
        player.Player.Inventory.Add(blade);
        if (VerificationFixtures.CreateBoardService().PlaceCard(player, blade.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var before = MatchSnapshot.From(player);
        var setup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(140));
        var result = new CombatSimulator().Simulate(setup);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, MatchSnapshot.From(opponent), result, before));
        playback.Advance(7);
        var first = playback.Project(SideId.Player).Cards.Single();
        playback.Skip();
        var final = playback.Project(SideId.Player).Cards.Single();
        var next = new CombatSimulator().Simulate(new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(1)));
        return first.CurrentValues[GameAttributeKeys.Multicast] == 1 && final.CurrentValues[GameAttributeKeys.Multicast] == 3
            && blade.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast) == 0
            && next.States[0].Cards.Single().Values[GameAttributeKeys.Multicast] == 0 && !result.PermanentChanges.Any();
    }

    private static CardBattleSetup Card(EntityId id, System.Collections.Generic.IReadOnlyList<AbilityDefinition> abilities) =>
        new(id, 0, 0, 70, abilities, UseLegacyAttack: false);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int timeout) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), enemy), 42, new BattleTick(timeout));
}
