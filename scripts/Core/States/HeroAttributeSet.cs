using Godot;

namespace Project_Star.Core.States;

// 英雄对局资源容器（全局层）：身份字段（get-only，构造注入）+ 金钱/经验/等级/声望。
public class HeroAttributeSet : AttributeSet
{
    // 身份字段（get-only，仅构造初始化）
    public StringName HeroKey { get; }
    public string HeroDisplayName { get; }
    public StringName FactionKey { get; }

    public int Wealth { get => GetValue(nameof(Wealth)); set => SetValue(nameof(Wealth), value); }
    public int Experience { get => GetValue(nameof(Experience)); set => SetValue(nameof(Experience), value); }
    public int Level { get => GetValue(nameof(Level)); set => SetValue(nameof(Level), value); }
    public int Reputation { get => GetValue(nameof(Reputation)); set => SetValue(nameof(Reputation), value); }

    // 注入身份字段构造英雄属性集（身份字段不可变，仅构造时初始化）。
    public HeroAttributeSet(StringName heroKey, string heroDisplayName, StringName factionKey)
    {
        HeroKey = heroKey;
        HeroDisplayName = heroDisplayName;
        FactionKey = factionKey;
    }
}