using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Factories;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Economy;

public sealed record SkillAcquisitionResult(
    SkillInstance Skill,
    bool WasCreated,
    int PreviousLevel,
    int CurrentLevel);

/// <summary>Grants a skill from any source and merges matching levels deterministically.</summary>
public sealed class SkillAcquisitionService
{
    private static readonly StringName UnsupportedLevel = new("skill.unsupported_level");
    private readonly EntityFactory _factory;

    public SkillAcquisitionService(EntityFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public Result<SkillAcquisitionResult> AcquireSkill(MatchSession session, SkillDefinition definition, int level)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.SupportsLevel(level))
            return Result<SkillAcquisitionResult>.Fail(new Failure(UnsupportedLevel, "Skill level is not configured."));

        var candidates = session.Player.Skills.Items
            .Select(skill => new MergeCandidate(
                skill.Id,
                skill.Attributes.Identity.Key,
                skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level)))
            .ToArray();
        var key = definition.Attributes.Identity.Key;
        var targetId = definition.SupportsLevel(level + 1)
            ? MergeDecision.FindTarget(key, level, candidates)
            : null;
        if (targetId is null)
        {
            var created = _factory.CreateSkill(definition, level);
            session.Player.Skills.Add(created);
            return Result<SkillAcquisitionResult>.Success(new SkillAcquisitionResult(created, true, level, level));
        }

        var excluded = new HashSet<EntityId> { targetId.Value };
        var consumed = new List<EntityId>();
        var currentLevel = level + 1;
        while (currentLevel < MergeDecision.MaximumMergeLevel && definition.SupportsLevel(currentLevel + 1))
        {
            var next = MergeDecision.FindTarget(key, currentLevel, candidates, excluded);
            if (next is null) break;
            consumed.Add(next.Value);
            excluded.Add(next.Value);
            currentLevel++;
        }

        var target = session.Player.Skills.Find(targetId.Value)!;
        foreach (var id in consumed) session.Player.Skills.Remove(id);
        _factory.ApplySkillLevel(target, definition, currentLevel);
        return Result<SkillAcquisitionResult>.Success(new SkillAcquisitionResult(target, false, level, currentLevel));
    }
}
