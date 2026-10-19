using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Monsters;

// 火史莱姆怪物的固定阵容、生命与技能定义（内容层）。
public sealed class FireSlimeMonsterDefinition : MonsterDefinition
{
    public FireSlimeMonsterDefinition()
        : base(new StringName("monster.fire_slime"), "火史莱姆",
            [
                new MonsterCardEntry(new StringName("card.lava_stone"), 1, 0),
                new MonsterCardEntry(new StringName("card.lava_stone"), 1, 1),
                new MonsterCardEntry(new StringName("card.fire_slime"), 1, 2),
            ], maxHealth: 40, level: 1,
            skills: [new MonsterSkillEntry(new StringName("skill.burn_mastery"), 1)],
            illustration: new StringName("res://art/ui/encounters/artwork/fire_slime-illustration-20261019.png")) { }
}
