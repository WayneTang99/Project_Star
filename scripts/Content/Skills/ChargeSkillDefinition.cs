using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 冲撞技能内容定义（内容层）。
public sealed class ChargeSkillDefinition : SkillDefinition
{
    public ChargeSkillDefinition()
        : base(
            new EntityAttributes<SkillIdentityAttributes>(
                new SkillIdentityAttributes(
                    new StringName("skill.charge"),
                    "冲撞",
                    GameFactions.Neutral)),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 10),
                CreateLevel(2, 20),
                CreateLevel(3, 30),
                CreateLevel(4, 40),
            ])
    {
    }

    private static SkillLevelDefinition CreateLevel(int level, int multiplier) => new(
        level,
        null,
        [new AbilityDefinition(
            new StringName("ability.charge"),
            AbilityActivation.EchoOnFirstAlliedCardActivated,
            AbilityTarget.EnemyHero,
            0,
            0,
            [new SourceHeroLevelScaledDamageEffectDefinition(multiplier)])]);
}
