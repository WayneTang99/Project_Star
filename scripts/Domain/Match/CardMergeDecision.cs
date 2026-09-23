using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Match;

// 合并决策使用的只读卡牌实例快照（对局领域层）。
public sealed record CardMergeCandidate(EntityId Id, StringName DefinitionKey, int Level);

// 以稳定 EntityId 顺序选择同 key、同等级卡牌（对局领域层）。
public static class CardMergeDecision
{
    public const int MaximumMergeLevel = 4;

    // 返回可以吸收新卡的稳定目标；满级或无候选时返回 null。
    public static EntityId? FindTarget(
        StringName definitionKey,
        int level,
        IReadOnlyList<CardMergeCandidate> candidates,
        IReadOnlySet<EntityId>? excluded = null)
    {
        if (level >= MaximumMergeLevel) return null;
        EntityId? selected = null;
        foreach (var candidate in candidates)
        {
            if (candidate.DefinitionKey != definitionKey || candidate.Level != level
                || (excluded?.Contains(candidate.Id) ?? false)) continue;
            if (selected is null || candidate.Id.Value.CompareTo(selected.Value.Value) < 0)
                selected = candidate.Id;
        }
        return selected;
    }
}
