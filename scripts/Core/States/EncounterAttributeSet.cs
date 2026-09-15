using Godot;

namespace Project_Star.Core.States;

// 遭遇对局资源容器（对局层）：身份字段（get-only，构造注入），暂无可变字段。
public class EncounterAttributeSet : AttributeSet
{
    // 身份字段（get-only，仅构造初始化）
    public StringName EncounterKey { get; }
    public string DisplayName { get; }

    // 注入身份字段构造遭遇属性集（身份字段不可变，仅构造时初始化）。
    public EncounterAttributeSet(StringName encounterKey, string displayName)
    {
        EncounterKey = encounterKey;
        DisplayName = displayName;
    }
}