using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Monsters;

// 咩咩1级怪物：银针草、咩咩羊与风车菊，携带治愈技能。
public sealed class BaaMonsterDefinition : MonsterDefinition
{
    public BaaMonsterDefinition()
        : base(new StringName("monster.baa"), "咩咩",
            [
                new MonsterCardEntry(new StringName("card.silver_needle_grass"), 2, 0),
                new MonsterCardEntry(new StringName("card.baa_sheep"), 1, 1),
                new MonsterCardEntry(new StringName("card.windmill_daisy"), 1, 2),
            ], level: 1,
            skills: [new MonsterSkillEntry(new StringName("skill.healing"), 1)],
            illustration: new StringName("res://art/ui/encounters/artwork/baa-illustration.png")) { }
}
