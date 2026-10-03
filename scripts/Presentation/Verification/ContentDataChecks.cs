using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 内容词条的只读隔离与构造边界验证（表现层验证模块）。
internal static class ContentDataChecks
{
    // 输入集合变更不影响只读身份；同类词条可重复，正文标点不影响分段。
    internal static bool DescriptionEntryBoundaries()
    {
        var entries = new List<CardDescriptionEntry>
        {
            new(CardKeywords.Activate, "原样保留；发动：这仍是正文。"),
            new(CardKeywords.Activate, "第二条发动。"), new(CardKeywords.Echo, "回响说明。"),
            new(CardKeywords.Aura, "光环说明。"), new(CardKeywords.Quest, "任务说明。"),
            new(CardKeywords.Pickup, "拾取说明。"), new(CardKeywords.Sell, "出售说明。"),
        };
        var identity = new CardIdentityAttributes(new StringName("card.description_fixture"), "描述夹具",
            GameFactions.Neutral, CardSize.Small, [GameElements.General], descriptionEntries: entries);
        entries.Clear();
        if (identity.DescriptionEntries.Count != 7
            || typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.DescriptionEntries))!.CanWrite
            || typeof(CardDescriptionEntry).GetProperty(nameof(CardDescriptionEntry.KeywordKey))!.CanWrite
            || typeof(CardDescriptionEntry).GetProperty(nameof(CardDescriptionEntry.Text))!.CanWrite) return false;
        try { ((IList<CardDescriptionEntry>)identity.DescriptionEntries).Clear(); return false; }
        catch (NotSupportedException) { }
        try { _ = new CardDescriptionEntry(new StringName("keyword.unknown"), "说明"); return false; }
        catch (ArgumentException) { }
        try { _ = new CardDescriptionEntry(CardKeywords.Activate, " "); return false; }
        catch (ArgumentException) { }
        return identity.DescriptionEntries.Select(entry => CardKeywords.DisplayName(entry.KeywordKey))
            .SequenceEqual(new[] { "发动", "发动", "回响", "光环", "任务", "拾取", "出售" });
    }
}
