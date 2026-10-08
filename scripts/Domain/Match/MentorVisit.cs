using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

// 单次导师访问中已经固定的技能候选（领域对局层）。
public sealed record MentorSkillOffer(StringName SkillKey, string DisplayName, int Level);

// 由对局拥有的导师选择状态；对外仅暴露只读候选（领域对局层）。
public sealed class MentorVisit
{
    internal MentorVisit(MentorIdentityAttributes identity, int level, IReadOnlyList<MentorSkillOffer> offers)
    {
        Identity = identity;
        Level = level;
        Offers = new List<MentorSkillOffer>(offers).AsReadOnly();
        IsResolved = Offers.Count == 0;
    }

    public MentorIdentityAttributes Identity { get; }
    public int Level { get; }
    public IReadOnlyList<MentorSkillOffer> Offers { get; }
    public bool IsResolved { get; private set; }
    internal void MarkResolved() => IsResolved = true;
}
