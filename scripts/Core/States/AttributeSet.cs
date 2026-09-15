using System.Collections.Generic;
using Godot;

namespace Project_Star.Core.States;

// 对局资源容器（全局层）：跨战斗保留的实体属性，数值以 StringName 为 key 存入字典，支持简单增减与复制。
public abstract class AttributeSet
{
    private Dictionary<StringName, int> _values = new();

    // 读取属性值；不存在返回 0。
    public int GetValue(StringName key) => _values.GetValueOrDefault(key, 0);

    // 覆盖设置属性值。
    public void SetValue(StringName key, int value) => _values[key] = value;

    // 累加属性值（数值流转统一走此方法，不直接改字段）。
    public void ApplyModifier(StringName key, int amount) => _values[key] = GetValue(key) + amount;

    // 扣减属性值（数值流转统一走此方法，不直接改字段）。
    public void RemoveModifier(StringName key, int amount) => _values[key] = GetValue(key) - amount;

    // 深拷贝属性集（供模板池复制独立实例时调用）。
    public virtual AttributeSet Clone()
    {
        var copy = (AttributeSet)MemberwiseClone();
        copy._values = new Dictionary<StringName, int>(_values);
        return copy;
    }
}