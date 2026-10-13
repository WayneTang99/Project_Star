using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 治愈无阵营属性光环定义（内容层）。
public sealed class HealingSkillDefinition : SkillDefinition
{
    public HealingSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.healing"), "治愈", GameFactions.Neutral,
                illustration: new StringName("res://art/ui/skills/artwork/healing-illustration.png"))),
            initialLevel: 1, levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.healing"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.HealingBonus, amount)])]);
}
