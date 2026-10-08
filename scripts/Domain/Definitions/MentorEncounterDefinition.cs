using System;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 引用独立导师的遭遇包装；等级决定本次导师及技能等级（领域定义层）。
public abstract class MentorEncounterDefinition : EncounterDefinition
{
    protected MentorEncounterDefinition(
        EntityAttributes<EncounterIdentityAttributes> attributes,
        StringName mentorKey,
        int level = 1,
        int minimumRound = 1,
        int maximumRound = 99,
        int baseWeight = 1)
        : base(attributes, minimumRound, maximumRound, EncounterKind.Other, baseWeight)
    {
        if (mentorKey.IsEmpty) throw new ArgumentException("Mentor key cannot be empty.", nameof(mentorKey));
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        MentorKey = mentorKey;
        Level = level;
    }

    public StringName MentorKey { get; }
    public int Level { get; }
}
