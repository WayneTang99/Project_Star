using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

// 只根据战场快照计算套装阈值，不修改对局状态（领域对局层）。
public sealed class CardSetEvaluator
{
    // 为同一套装阈值生成固定能力与 Modifier 来源身份。
    public static EntityId SourceId(StringName setKey, int threshold)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"set:{setKey}:{threshold}"));
        return new EntityId(new Guid(hash.AsSpan(0, 16)));
    }

    // 按战场卡牌 key 去重并稳定返回所有已达到的套装阈值。
    public IReadOnlyList<ActiveCardSetThreshold> Evaluate(
        MatchSession session,
        IReadOnlyDictionary<StringName, CardSetDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definitions);
        var cardKeysBySet = new Dictionary<StringName, HashSet<StringName>>();
        foreach (var placement in session.Board.Battlefield.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Battlefield card '{placement.CardId}' is not owned.");
            var setKey = card.Attributes.Identity.SetKey;
            if (setKey is null) continue;
            if (!definitions.ContainsKey(setKey))
                throw new InvalidOperationException($"Card '{card.Attributes.Identity.Key}' references unknown set '{setKey}'.");
            if (!cardKeysBySet.TryGetValue(setKey, out var cardKeys))
                cardKeysBySet.Add(setKey, cardKeys = []);
            cardKeys.Add(card.Attributes.Identity.Key);
        }

        var active = new List<ActiveCardSetThreshold>();
        foreach (var (setKey, cardKeys) in cardKeysBySet.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            foreach (var threshold in definitions[setKey].Thresholds)
                if (cardKeys.Count >= threshold.RequiredDistinctCards)
                    active.Add(new ActiveCardSetThreshold(setKey, threshold));
        }
        return active.AsReadOnly();
    }
}

// 一条已激活的套装阈值（领域对局层）。
public sealed record ActiveCardSetThreshold(
    StringName SetKey,
    CardSetThresholdDefinition Threshold);
