using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 体能训练可选项遭遇内容定义（内容层）。
public sealed class PhysicalTrainingEncounterDefinition : ChoiceEncounterDefinition
{
    public PhysicalTrainingEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.physical_training"), "体能训练")),
            1,
            99,
            [
                new EncounterOptionDefinition(
                    new StringName("encounter.physical_training.max_health"),
                    "强化体魄",
                    [new ModifyHeroCombatAttributeByLevelEffectDefinition(GameAttributeKeys.MaxHealth, 10)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.physical_training.health_regen"),
                    "耐力训练",
                    [new ModifyHeroCombatAttributeByLevelEffectDefinition(GameAttributeKeys.HealthRegen, 1)]),
            ],
            level: 1)
    {
    }
}
