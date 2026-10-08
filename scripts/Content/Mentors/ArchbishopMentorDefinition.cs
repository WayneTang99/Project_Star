using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Mentors;

// 传授圣骑士归属技能的大主教导师（内容层）。
public sealed class ArchbishopMentorDefinition : MentorDefinition
{
    public ArchbishopMentorDefinition()
        : base(new EntityAttributes<MentorIdentityAttributes>(new MentorIdentityAttributes(
            new StringName("mentor.archbishop"), "大主教", summary: "传授圣骑士技能")), level: 1)
    {
    }

    // 筛选技能归属，不以拜访导师的英雄归属替代圣骑士归属。
    public override bool CanOfferSkill(SkillDefinition skill, HeroIdentityAttributes hero) =>
        skill.Attributes.Identity.FactionKey == new StringName("paladin");
}
