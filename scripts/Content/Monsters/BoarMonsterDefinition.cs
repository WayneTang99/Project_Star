using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Monsters;

// 野猪怪物内容定义（内容层）。
public sealed class BoarMonsterDefinition : MonsterDefinition
{
    public BoarMonsterDefinition()
        : base(
            new StringName("monster.boar"),
            "野猪",
            [
                new MonsterCardEntry(new StringName("card.beast_hide"), 1, 0),
                new MonsterCardEntry(new StringName("card.beast_hide"), 1, 1),
                new MonsterCardEntry(new StringName("card.boar"), 1, 2),
            ],
            level: 1)
    {
    }
}
