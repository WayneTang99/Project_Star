using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Domain.Combat;

// 开战前冻结各等级基础值和能力，Runtime不访问可变对局实例。
public sealed record BattleCardLevel(IReadOnlyDictionary<StringName, int> Values, IReadOnlyList<AbilityDefinition> Abilities);

public static class BattleCardLevels
{
    public static IReadOnlyDictionary<int, BattleCardLevel> Capture(CardDefinition? definition, CardInstance? card = null)
    {
        var levels = new Dictionary<int, BattleCardLevel>();
        if (definition is null) return levels;
        for (var level = definition.InitialLevel; level <= 4; level++)
        {
            if (!definition.SupportsLevel(level)) continue;
            var values = definition.Attributes.BaseCombat.CreateMutableCopy();
            var configured = definition.GetLevel(level);
            if (configured is not null) foreach (var entry in configured.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
            var abilities = configured?.Abilities ?? definition.Abilities;
            levels[level] = new BattleCardLevel(values.SnapshotFinalValues(), card is null ? abilities : CardAbilityComposer.Compose(card, abilities));
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, BattleCardLevel>(levels);
    }
}
