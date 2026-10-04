using System;
using Godot;

namespace Project_Star.Domain.Definitions;

// 描述词条只组织展示文本，能力与触发规则仍由能力定义提供。
public static class CardKeywords
{
    public static readonly StringName Activate = new("keyword.activate");
    public static readonly StringName Echo = new("keyword.echo");
    public static readonly StringName Aura = new("keyword.aura");
    public static readonly StringName Quest = new("keyword.quest");
    public static readonly StringName Pickup = new("keyword.pickup");
    public static readonly StringName Sell = new("keyword.sell");
    public static readonly StringName Consume = new("keyword.consume");
    public static readonly StringName Passive = new("keyword.passive");
    public static readonly StringName Summon = new("keyword.summon");

    public static string DisplayName(StringName key) => key == Activate ? "发动"
        : key == Echo ? "回响" : key == Aura ? "光环" : key == Quest ? "任务"
        : key == Pickup ? "拾取" : key == Sell ? "出售" : key == Consume ? "消耗" : key == Passive ? "被动"
        : key == Summon ? "召唤"
        : throw new ArgumentException($"Unknown description keyword: {key}", nameof(key));
}

public sealed record CardDescriptionEntry
{
    public CardDescriptionEntry(StringName keywordKey, string text)
    {
        _ = CardKeywords.DisplayName(keywordKey);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Description text cannot be empty.", nameof(text));
        KeywordKey = keywordKey;
        Text = text;
    }

    public StringName KeywordKey { get; }
    public string Text { get; }
}
