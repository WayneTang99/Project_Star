using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 校场固定双选项遭遇（内容层）。
public sealed class TrainingGroundEncounterDefinition : ChoiceEncounterDefinition
{
    public TrainingGroundEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.training_ground"), "校场")),
            1,
            99,
            [
                new EncounterOptionDefinition(
                    new StringName("encounter.training_ground.body_training"),
                    "体能训练",
                    [new ModifyHeroCombatAttributeByLevelEffectDefinition(GameAttributeKeys.MaxHealth, 10)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.training_ground.sparring"),
                    "对阵训练",
                    [new ModifyBattlefieldCardAttributeEncounterOptionEffectDefinition(GameAttributeKeys.AttackDamage, 5)]),
            ],
            level: 1)
    {
    }
}
