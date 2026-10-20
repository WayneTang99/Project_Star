using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 训练人类攻击光环技能定义（内容层）。
public sealed class TrainingSkillDefinition : SkillDefinition
{
    public TrainingSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.training"), "训练", new StringName("paladin"),
                illustration: new StringName("res://art/ui/skills/artwork/training-human-redraw-20261020.png"))),
            initialLevel: 1, levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.training"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, amount, RequiredTag: GameTags.Human)])]);
}
