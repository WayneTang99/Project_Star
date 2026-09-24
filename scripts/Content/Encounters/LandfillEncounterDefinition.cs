using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 垃圾场固定双选项遭遇内容定义（内容层）。
public sealed class LandfillEncounterDefinition : ChoiceEncounterDefinition
{
    public LandfillEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.landfill"), "垃圾场")),
            1,
            99,
            [
                new EncounterOptionDefinition(
                    new StringName("encounter.landfill.wealth"),
                    "拾取零钱",
                    [new GainWealthEncounterOptionEffectDefinition(2)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.landfill.material_small_card"),
                    "变废为宝",
                    [new GrantRandomTaggedCardEncounterOptionEffectDefinition(GameTags.Material, CardSize.Small, 1)]),
            ],
            [
                new EncounterOptionSlotDefinition(
                    [new WeightedEncounterOptionDefinition(new StringName("encounter.landfill.wealth"), 1)]),
                new EncounterOptionSlotDefinition(
                    [new WeightedEncounterOptionDefinition(new StringName("encounter.landfill.material_small_card"), 1)]),
            ],
            level: 1)
    {
    }
}
