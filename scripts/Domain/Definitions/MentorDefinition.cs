using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 独立导师身份、默认等级与技能筛选入口（领域定义层）。
public abstract class MentorDefinition
{
    protected MentorDefinition(EntityAttributes<MentorIdentityAttributes> attributes, int level = 1)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        DefinitionFreezer.Freeze(attributes);
    }

    public EntityAttributes<MentorIdentityAttributes> Attributes { get; }
    public int Level { get; }

    // 内容定义实现纯筛选判断；抽选、等级校验和领取由通用用例处理。
    public abstract bool CanOfferSkill(SkillDefinition skill, HeroIdentityAttributes hero);
}
