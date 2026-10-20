using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 复生仪式小魔女技能：己方小型卡牌摧毁后在原位置召唤同级骷髅（内容层）。
public sealed class RebirthRitualSkillDefinition : SkillDefinition
{
    public RebirthRitualSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(
            new SkillIdentityAttributes(new StringName("skill.rebirth_ritual"), "复生仪式", new StringName("mona"),
                illustration: new StringName("res://art/ui/skills/artwork/rebirth_ritual-illustration.png"))),
            initialLevel: 4, levels: [new SkillLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.summon_on_allied_destruction"),
                    AbilityActivation.EchoOnAlliedCardDestroyed, AbilityTarget.AlliedHero, 0, 0,
                    [new SummonAtDestroyedCardEffectDefinition(new SkeletonCardDefinition())], triggerCardSize: CardSize.Small)])]) { }
}
