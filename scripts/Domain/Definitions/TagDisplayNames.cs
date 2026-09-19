using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Definitions;

public static class TagDisplayNames
{
    private static readonly Dictionary<StringName, string> Names = new()
    {
        [GameTags.Small] = "小型",
        [GameTags.Medium] = "中型",
        [GameTags.Large] = "大型",
        [GameTags.Weapon] = "武器",
        [GameTags.Clothing] = "服饰",
        [GameTags.Consumable] = "消耗品",
        [GameTags.Human] = "人类",
        [GameTags.Mechanical] = "机械",
    };

    public static string Get(StringName tag) => Names.TryGetValue(tag, out var name) ? name : tag.ToString();
}
