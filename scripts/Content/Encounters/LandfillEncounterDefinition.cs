using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 垃圾填埋场加权选项遭遇内容定义（内容层）。
public sealed class LandfillEncounterDefinition : ChoiceEncounterDefinition
{
    public LandfillEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.landfill"), "垃圾填埋场")),
            1,
            99,
            [
                new EncounterOptionDefinition(
                    new StringName("encounter.landfill.wealth"),
                    "获得2金币",
                    [new GainWealthEncounterOptionEffectDefinition(2)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.landfill.faction_small_card"),
                    "获得本职业随机一张1级小型卡牌",
                    [new GrantRandomFactionCardEncounterOptionEffectDefinition(CardSize.Small, 1)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.landfill.material_small_card"),
                    "获得随机一张1级小型材料卡牌",
                    [new GrantRandomTaggedCardEncounterOptionEffectDefinition(GameTags.Material, CardSize.Small, 1)]),
            ],
            [
                new EncounterOptionSlotDefinition(
                    [new WeightedEncounterOptionDefinition(new StringName("encounter.landfill.wealth"), 1)]),
                new EncounterOptionSlotDefinition(
                [
                    new WeightedEncounterOptionDefinition(new StringName("encounter.landfill.faction_small_card"), 60),
                    new WeightedEncounterOptionDefinition(new StringName("encounter.landfill.material_small_card"), 40),
                ]),
            ],
            level: 1)
    {
    }
}
