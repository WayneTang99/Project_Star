using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 魔导士套装的胜利与英雄升级技能奖励（内容层）。
public sealed class MagisterSetDefinition : CardSetDefinition
{
    public MagisterSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.magister"), "魔导士")),
        [new CardSetThresholdDefinition(2,
            [new AbilityDefinition(new StringName("ability.magister_victory_skill"), AbilityActivation.PassiveOnBattleVictory,
                AbilityTarget.AlliedHero, 0, 0, [new GrantRandomFactionSkillEffectDefinition(new StringName("mona"), 1)])]),
         new CardSetThresholdDefinition(3,
            [new AbilityDefinition(new StringName("ability.magister_upgrade_skill"), AbilityActivation.PassiveOnHeroLevelUp,
                AbilityTarget.AlliedHero, 0, 0, [new GrantRandomFactionSkillEffectDefinition(new StringName("mona"), 2)])])]) { }
}
