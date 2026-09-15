using System.Collections.Generic;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.States;

// 卡牌对局资源容器（全局层）：身份字段（get-only，构造注入）+ 等级/价值/对局加成/解锁记录。
public class CardAttributeSet : AttributeSet
{
    // 身份字段（get-only，仅构造初始化）
    public StringName CardKey { get; }
    public string DisplayName { get; }
    public StringName FactionKey { get; }
    public CardSize Size { get; }

    public int Level { get => GetValue(nameof(Level)); set => SetValue(nameof(Level), value); }
    public int Value { get => GetValue(nameof(Value)); set => SetValue(nameof(Value), value); }
    public int PersistentBonus { get => GetValue(nameof(PersistentBonus)); set => SetValue(nameof(PersistentBonus), value); }

    // 解锁记录（对局内按 key 跟踪，如任务解锁）。
    public HashSet<StringName> UnlockRecord { get; } = new();

    // 注入身份字段构造卡牌属性集（身份字段不可变，仅构造时初始化）。
    public CardAttributeSet(StringName cardKey, string displayName, StringName factionKey, CardSize size)
    {
        CardKey = cardKey;
        DisplayName = displayName;
        FactionKey = factionKey;
        Size = size;
    }

    // 深拷贝卡牌属性集（含解锁记录，供模板池复制实例时调用）。
    public override AttributeSet Clone()
    {
        var copy = (CardAttributeSet)base.Clone();
        copy.UnlockRecord.Clear();
        foreach (var key in UnlockRecord)
            copy.UnlockRecord.Add(key);
        return copy;
    }
}