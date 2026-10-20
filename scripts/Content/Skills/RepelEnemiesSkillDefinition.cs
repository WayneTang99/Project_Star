using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 御敌人类护甲光环技能定义（内容层）。
public sealed class RepelEnemiesSkillDefinition : SkillDefinition
{
    public RepelEnemiesSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.repel_enemies"), "御敌", new StringName("paladin"),
                illustration: new StringName("res://art/ui/skills/artwork/repel_enemies-human-redraw-20261020.png"))),
            initialLevel: 1, levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.repel_enemies"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.Armor, amount, RequiredTag: GameTags.Human)])]);
}
