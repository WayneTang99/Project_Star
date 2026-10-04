using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 光属性冷却光环的分级、叠加、失效与永久倍率组合验证（表现层）。
internal static class RadiantStandardChecks
{
    internal static bool LevelsAndFiltering()
    {
        var definition = new RadiantStandardCardDefinition();
        if (definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.ToArray().SequenceEqual(new[] { GameTags.Small })) return false;
        for (var level = 3; level <= 4; level++)
        {
            var receiver = Attacker(0, GameElements.Light);
            var normal = Attacker(1, GameElements.General);
            var bench = Attacker(0, GameElements.Light) with { IsOnBench = true };
            var enemy = Attacker(0, GameElements.Light);
            var setup = Battle([receiver, normal, bench, Banner(level, 2), Banner(4, 0) with { IsOnBench = true }], [enemy], 200);
            var result = new CombatSimulator().Simulate(setup);
            var ticks = level == 3 ? 93 : 85;
            if (!result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == receiver.EntityId)
                .Select(e => e.Tick.Value).SequenceEqual(new long[] { ticks, ticks * 2 })
                || result.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != ticks * 2
                || result.States[0].Cards.Single(card => card.Id == normal.EntityId).CooldownDurationUnits.Single() != 200
                || result.States[0].Cards.Single(card => card.Id == bench.EntityId).CooldownDurationUnits.Single() != 200
                || result.States[0].Cards.Single(card => card.Id == enemy.EntityId).CooldownDurationUnits.Single() != 200
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Count != 0) return false;
        }
        return true;
    }

    internal static bool StackingRemovalAndMultiplier()
    {
        var receiver = Attacker(0, GameElements.Light);
        var first = Banner(3, 1);
        first = first with { ElementKeys = [GameElements.General], Abilities = first.Abilities!.Append(new AbilityDefinition(new StringName("verification.radiant.destroy"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var setup = Battle([receiver, first, Banner(4, 2)], [], 100);
        var result = new CombatSimulator().Simulate(setup);
        if (result.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != 156
            || result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == receiver.EntityId).Tick.Value != 85
            || result.States.Last(state => state.Tick.Value == 20).Cards.Single(card => card.Id == receiver.EntityId)
                .CooldownDurationUnits.Single() != 170) return false;
        receiver = receiver with { CooldownMultiplier = 0.8m };
        result = new CombatSimulator().Simulate(Battle([receiver, Banner(3, 1), Banner(4, 2)], [], 130));
        if (result.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != 124.8m
            || !result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == receiver.EntityId)
                .Select(e => e.Tick.Value).SequenceEqual(new long[] { 63, 126 })) return false;
        // 叠加到100%时每Tick至多发动一次，光环不改写输入倍率。
        var banners = Enumerable.Range(1, 7).Select(start => Banner(4, start)).Prepend(receiver).ToArray();
        result = new CombatSimulator().Simulate(Battle(banners, [], 3));
        return result.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() == 0
            && result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == receiver.EntityId) == 3
            && receiver.CooldownMultiplier == 0.8m && result.PermanentChanges.Count == 0;
    }

    private static CardBattleSetup Attacker(int start, StringName element) => new(EntityId.New(), start, 1, 100,
        [new AbilityDefinition(new StringName("verification.radiant.attack"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 100, [new DamageEffectDefinition(1)])], UseLegacyAttack: false, ElementKeys: [element]);

    private static CardBattleSetup Banner(int level, int start) => new(EntityId.New(), start, 0, 0,
        new RadiantStandardCardDefinition().GetLevel(level)!.Abilities, UseLegacyAttack: false, ElementKeys: [GameElements.Light]) { Level = level };

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemies), 42,
        new BattleTick(ticks), new BattleTick(ticks + 1));
}
