using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 左手刀无阵营属性光环定义（内容层）。
public sealed class LeftHandBladeSkillDefinition : SkillDefinition
{
    public LeftHandBladeSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.left_hand_blade"), "左手刀", GameFactions.Neutral,
                illustration: new StringName("res://art/ui/skills/artwork/left_hand_blade-illustration.png"))),
            initialLevel: 1, levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.left_hand_blade"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, amount, ExtremeSide: AdjacentCardSide.Left)])]);
}
