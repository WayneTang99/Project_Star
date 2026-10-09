using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 捍卫技能内容定义（内容层）。
public sealed class DefendSkillDefinition : SkillDefinition
{
    public DefendSkillDefinition()
        : base(
            new EntityAttributes<SkillIdentityAttributes>(
                new SkillIdentityAttributes(
                    new StringName("skill.defend"),
                    "捍卫",
                    new StringName("paladin"),
                    illustration: new StringName("res://art/ui/skills/artwork/defend-illustration.png"))),
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
            new StringName("ability.defend"),
            AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.AlliedHero,
            0,
            0,
            [new GainSourceHeroLevelScaledArmorEffectDefinition(multiplier)])]);
}
