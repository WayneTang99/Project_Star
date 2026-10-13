using System.Collections.Generic;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 探险遭遇共用固定选项与加权结果，沿用普通事件排程和结算。
public abstract class ExplorationEncounterDefinition : ChoiceEncounterDefinition
{
    protected ExplorationEncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        int minimumRound,
        int maximumRound,
        IReadOnlyList<EncounterOptionDefinition> options,
        int level,
        int baseWeight = 1)
        : base(attributes, minimumRound, maximumRound, options, level: level, baseWeight: baseWeight) { }
}
