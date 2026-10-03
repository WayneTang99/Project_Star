using System;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 破誓者随机多目标与动态冷却光环的实际战斗验证（表现层验证模块）。
internal static class OathbreakerChecks
{
    internal static bool TargetsAndAura()
    {
        var definition = new OathbreakerCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "破誓者" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || !definition.Tags.Contains(GameTags.Human) || definition.InitialLevel != 1) return false;
        var sourceId = EntityId.New();
        var targets = Enumerable.Range(0, 5).Select(index => Card(index)).ToArray();
        for (var level = 1; level <= 4; level++)
        {
            var source = Card(0) with { EntityId = sourceId, Abilities = definition.GetLevel(level)!.Abilities };
            var setup = Battle([source], targets, 30);
            var result = new CombatSimulator().Simulate(setup);
            var slowed = result.States[^1].Cards.Where(card => card.Slow > 0).ToArray();
            if (slowed.Length != level || slowed.Any(card => card.Slow != 10 || card.Side != SideId.Opponent)
                || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var fewer = new CombatSimulator().Simulate(Battle([source], targets.Take(1).ToArray(), 30));
            if (fewer.States[^1].Cards.Count(card => card.Slow == 10) != 1) return false;
        }
        var breaker = Card(0) with { EntityId = sourceId, Abilities = definition.GetLevel(4)!.Abilities };
        var allyLight = Card(2) with { ElementKeys = [GameElements.Light] };
        var enemyLight = Card(0) with { ElementKeys = [GameElements.Light] };
        var benchLight = Card(1) with { ElementKeys = [GameElements.Light], IsOnBench = true };
        var fixedSetup = Battle([breaker, allyLight], [enemyLight, benchLight], 100);
        var fixedResult = new CombatSimulator().Simulate(fixedSetup);
        if (!fixedResult.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == sourceId)
            .Select(item => item.Tick.Value).SequenceEqual(new long[] { 50, 100 })) return false;
        var destroy = new AbilityDefinition(new StringName("verification.light_destroy"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)]);
        var dynamicResult = new CombatSimulator().Simulate(Battle([breaker, allyLight],
            [enemyLight with { Abilities = [destroy] }, benchLight], 40));
        var afterDestruction = dynamicResult.States.Last(frame => frame.Tick.Value == 20).Cards.Single(card => card.Id == sourceId);
        if (afterDestruction.CooldownUnits.Single() != 40 || afterDestruction.CooldownDurationUnits.Single() != 80
            || dynamicResult.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == sourceId).Tick.Value != 40)
            return false;
        var empty = new CombatSimulator().Simulate(Battle([breaker], [benchLight], 30));
        if (empty.Events.OfType<StatusChangedEvent>().Any()) return false;
        var scaled = breaker with { CooldownMultiplier = .5m };
        var scaledResult = new CombatSimulator().Simulate(Battle([scaled, allyLight], [enemyLight], 25));
        return scaledResult.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == sourceId).Tick.Value == 25;
    }

    private static CardBattleSetup Card(int position) => new(EntityId.New(), position, 0, 0, [], UseLegacyAttack: false);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int timeout) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemy), 42, new BattleTick(timeout));
}
