using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Monsters;

// 越狱者固定属性与卡组定义（内容层）。
public sealed class JailbreakerMonsterDefinition : MonsterDefinition
{
    public JailbreakerMonsterDefinition()
        : base(new StringName("monster.jailbreaker"), "越狱者",
            [
                new MonsterCardEntry(new StringName("card.lockpick"), 2, 0),
                new MonsterCardEntry(new StringName("card.oathbreaker"), 1, 1),
            ], maxHealth: 60, level: 1,
            skills: [new MonsterSkillEntry(new StringName("skill.banish"), 2)],
            illustration: new StringName("res://art/ui/encounters/artwork/jailbreaker-illustration.png"))
    {
    }
}
