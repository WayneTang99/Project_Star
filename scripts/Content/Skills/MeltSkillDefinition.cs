using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 消融小魔女技能，复用一次性元素发动联动窗口（内容层）。
public sealed class MeltSkillDefinition : SkillDefinition
{
    public MeltSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(new SkillIdentityAttributes(
            new StringName("skill.melt"), "消融", new StringName("mona"),
            illustration: new StringName("res://art/ui/skills/artwork/melt-illustration.png"))),
            initialLevel: 3, levels: [CreateLevel(3), CreateLevel(4)]) { }

    private static SkillLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.melt_sequence"), AbilityActivation.PassiveAura,
            AbilityTarget.EnemyHero, 0, 0,
            [new ElementSequenceEffectDefinition(GameElements.Ice, GameElements.Fire,
                (level - 1) * 10, RequiresAttack: true, DamageMultiplier: 1.5m)])]);
}
