using Godot;

namespace Project_Star.Core.States;

// 对局资源容器（全局层）：跨战斗保留的实体属性，数值以 StringName 为 key 存入字典，支持简单增减与复制。
public abstract class AttributeSet : StateContainer
{
    // 深拷贝属性集（供模板池复制独立实例时调用）。
    public override StateContainer Clone()
    {
        var copy = (AttributeSet)MemberwiseClone();
        return copy;
    }
}
