using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 放逐无阵营技能定义，战斗开始时随机禁锢双方战场卡牌（内容层）。
public sealed class BanishSkillDefinition : SkillDefinition
{
    public BanishSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.banish"), "放逐", GameFactions.Neutral,
                illustration: new StringName("res://art/ui/skills/artwork/banish-illustration.png"))),
            initialLevel: 2, levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)])
    {
    }

    private static SkillLevelDefinition CreateLevel(int level, int durationTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.opening_random_immobilize"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0,
            [
                new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.ImmobilizeDuration, durationTicks),
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, durationTicks),
            ])]);
}
