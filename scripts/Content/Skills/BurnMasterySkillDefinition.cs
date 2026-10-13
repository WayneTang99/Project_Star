using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 灼伤精通无阵营属性光环定义（内容层）。
public sealed class BurnMasterySkillDefinition : SkillDefinition
{
    public BurnMasterySkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.burn_mastery"), "灼伤精通", GameFactions.Neutral,
                illustration: new StringName("res://art/ui/skills/artwork/burn_mastery-illustration.png"))),
            initialLevel: 1, levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.burn_mastery"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.BurnBonus, amount)])]);
}
