using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 将独立怪物定义投影成遭遇，固定怪物回合与探险使用同一身份和等级。
public sealed class MonsterEncounterDefinition : EncounterDefinition
{
    public MonsterEncounterDefinition(MonsterDefinition monster)
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            monster.Attributes.Identity.Key, monster.Attributes.Identity.DisplayName, monster.Attributes.Identity.Illustration)),
            monster.MinimumRound, monster.MaximumRound, EncounterKind.Monster)
    {
        Level = monster.Level;
    }

    public int Level { get; }
}
