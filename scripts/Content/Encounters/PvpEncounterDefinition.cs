using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 每轮第8回合使用的异步对战遭遇（内容层）。
public sealed class PvpEncounterDefinition : EncounterDefinition
{
    public PvpEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.pvp"), "异步对战")),
            1,
            99,
            EncounterKind.Pvp)
    {
    }
}
