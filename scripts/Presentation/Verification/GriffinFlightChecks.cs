using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 神圣狮鹫飞行目标、疾速范围与确定性验证（表现层验证模块）。
internal static class GriffinFlightChecks
{
    internal static bool TargetsAndReplay()
    {
        var left = EntityId.New(); var griffin = EntityId.New(); var right = EntityId.New();
        var distant = EntityId.New(); var bench = EntityId.New(); var enemy = EntityId.New();
        var definition = new HolyGriffinCardDefinition();
        CardBattleSetup Human(EntityId id, int start, bool onBench = false) =>
            new(id, start, 5, 100, [], IsOnBench: onBench, UseLegacyAttack: false,
                Tags: new TagSet([GameTags.Human]));
        BattleSetup Setup(int level, ulong seed, bool rightHuman, bool includeLeft) => new(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0),
                (includeLeft ? new[] { Human(left, 0) } : System.Array.Empty<CardBattleSetup>()).Concat(new[]
                {
                    new CardBattleSetup(griffin, 1, 0, 50, definition.GetLevel(level)!.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3),
                    rightHuman ? Human(right, 4) : new CardBattleSetup(right, 4, 0, 100, [], UseLegacyAttack: false),
                    Human(distant, 6), Human(bench, 0, true),
                }).ToArray()),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0), [Human(enemy, 0)]),
            seed, new BattleTick(50));
        var selections = new System.Collections.Generic.HashSet<EntityId>();
        for (var level = 2; level <= 4; level++)
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Setup(level, seed, true, true);
            var simulator = new CombatSimulator();
            var result = simulator.Simulate(setup);
            var states = result.States[^1].Cards;
            if (!states.Single(card => card.Id == griffin).IsFlying
                || states.Count(card => card.Id != griffin && card.IsFlying) != 1
                || states.Single(card => card.Id == left).Haste != 10
                || states.Single(card => card.Id == right).Haste != 10
                || states.Any(card => (card.Id == distant || card.Id == bench || card.Id == enemy)
                    && (card.IsFlying || card.Haste != 0))
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
            selections.Add(states.Single(card => card.Id != griffin && card.IsFlying).Id);
        }
        if (selections.Count != 2) return false;
        var mixed = new CombatSimulator().Simulate(Setup(2, 1, false, true)).States[^1].Cards;
        if (!mixed.Single(card => card.Id == left).IsFlying || mixed.Single(card => card.Id == right).IsFlying
            || mixed.Single(card => card.Id == right).Haste != 10) return false;
        var alone = new CombatSimulator().Simulate(Setup(2, 1, false, false)).States[^1].Cards;
        return alone.Single(card => card.Id == griffin).IsFlying
            && alone.Count(card => card.IsFlying) == 1 && alone.Single(card => card.Id == right).Haste == 10;
    }
}
