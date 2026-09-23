using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 突袭技能内容定义（内容层）。
public sealed class AssaultSkillDefinition : SkillDefinition
{
    public AssaultSkillDefinition()
        : base(
            new EntityAttributes<SkillIdentityAttributes>(
                new SkillIdentityAttributes(
                    new StringName("skill.assault"),
                    "突袭",
                    GameFactions.Neutral)),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 5),
                CreateLevel(2, 10),
                CreateLevel(3, 15),
                CreateLevel(4, 20),
            ])
    {
    }

    private static SkillLevelDefinition CreateLevel(int level, int percent) => new(
        level,
        null,
        [new AbilityDefinition(
            new StringName("ability.assault"),
            AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero,
            0,
            0,
            [new MaxHealthPercentDamageEffectDefinition(percent)])]);
}
