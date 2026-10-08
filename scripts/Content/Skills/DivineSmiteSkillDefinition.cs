using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 至圣斩技能内容定义（内容层）。
public sealed class DivineSmiteSkillDefinition : SkillDefinition
{
    public DivineSmiteSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.divine_smite"), "至圣斩", new StringName("paladin"))),
            initialLevel: 4,
            levels:
            [new SkillLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.divine_smite"), AbilityActivation.PassiveAura,
                    AbilityTarget.AlliedHero, 0, 0,
                    [
                        new MultiplyAlliedElementCardAttributeAuraEffectDefinition(GameElements.Light, GameAttributeKeys.AttackDamage, 2),
                        new MultiplyAlliedElementCardAttributeAuraEffectDefinition(GameElements.Light, GameAttributeKeys.AttackDamage, 2,
                            [GameTags.Demon, GameTags.Undead]),
                    ])])])
    {
    }
}
