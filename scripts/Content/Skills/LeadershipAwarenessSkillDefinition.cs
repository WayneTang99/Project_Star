using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 领袖意识技能内容定义（内容层）。
public sealed class LeadershipAwarenessSkillDefinition : SkillDefinition
{
    public LeadershipAwarenessSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.leadership_awareness"), "领袖意识", new StringName("paladin"),
                illustration: new StringName("res://art/ui/skills/artwork/leadership_awareness-illustration.png"))),
            initialLevel: 3, levels: [CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static SkillLevelDefinition CreateLevel(int level, int chargeTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.leadership_awareness"), AbilityActivation.EchoOnFirstAlliedCardActivated,
            AbilityTarget.AlliedHero, 0, 0,
            [new ChargeTaggedAlliedCardsEffectDefinition(GameTags.Human, chargeTicks, ExcludeEventCard: true)],
            triggerCardTag: GameTags.Human)]);
}
