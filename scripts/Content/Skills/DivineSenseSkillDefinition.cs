using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 神圣感知圣骑士技能定义，开战随机将敌方卡牌视为恶魔（内容层）。
public sealed class DivineSenseSkillDefinition : SkillDefinition
{
    public DivineSenseSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.divine_sense"), "神圣感知", new StringName("paladin"),
                illustration: new StringName("res://art/ui/skills/artwork/divine_sense-illustration.png"))),
            initialLevel: 3, levels: [CreateLevel(3, 1), CreateLevel(4, 2)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int count) => new(level, null,
        [new AbilityDefinition("ability.opening_random_enemy_tag", AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero, 0, 0, [new GrantTagToRandomEnemyCardsEffectDefinition(GameTags.Demon, count)])]);
}
