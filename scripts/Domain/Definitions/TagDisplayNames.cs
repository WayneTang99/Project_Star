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
        [GameTags.Equipment] = "装备",
        [GameTags.Material] = "材料",
        [GameTags.Consumable] = "消耗品",
        [GameTags.Human] = "人类",
        [GameTags.Mechanical] = "机械",
        [GameTags.Location] = "地域",
    };

    public static string Get(StringName tag) => Names.TryGetValue(tag, out var name) ? name : tag.ToString();
}
