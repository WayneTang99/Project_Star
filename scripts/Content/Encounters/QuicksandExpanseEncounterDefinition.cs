using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 流沙地带探险：淘金或穿越沙暴，奖励随本次遭遇等级。
public sealed class QuicksandExpanseEncounterDefinition : ExplorationEncounterDefinition
{
    public QuicksandExpanseEncounterDefinition()
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            new StringName("encounter.quicksand_expanse"), "流沙地带",
            new StringName("res://art/ui/encounters/artwork/quicksand_expanse-illustration.png"), "淘金，或穿越沙暴寻找宝藏")),
            1, 99,
            [
                new EncounterOptionDefinition(new StringName("encounter.quicksand_expanse.pan_gold"), "淘金",
                    [new GainRandomWealthEncounterOptionEffectDefinition(2, 8)]),
                new EncounterOptionDefinition(new StringName("encounter.quicksand_expanse.cross_sandstorm"), "穿越沙暴",
                    [new WeightedEncounterOptionEffectDefinition(
                    [
                        new(new GrantRandomFactionCardEncounterOptionEffectDefinition(CardSize.Medium, 2, UseEncounterLevel: true), 60),
                        new(new EnterRandomMonsterEncounterOptionEffectDefinition(), 30),
                        new(new GrantCardEncounterOptionEffectDefinition(new StringName("card.jewelry_bag"), 2, UseEncounterLevel: true), 10),
                    ])]),
            ], level: 2) { }
}
