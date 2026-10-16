using System;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Playtest;

// 选角和图鉴共用的归属展示顺序（表现层）。
internal static class FactionDisplayOrder
{
    private static readonly StringName[] Keys =
    [GameFactions.Neutral, new("paladin"), new("mona"), new("robin"), new("harla"), new("valos"), new("jiyun")];

    // 英雄身份key和内容归属使用相同的展示优先级，未知归属排在末尾。
    internal static int Rank(StringName key)
    {
        var value = key.ToString();
        var faction = value.StartsWith("hero.", StringComparison.Ordinal) ? new StringName(value[5..]) : key;
        var index = Array.IndexOf(Keys, faction);
        return index < 0 ? Keys.Length : index;
    }
}
