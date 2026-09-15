using System.Collections.Generic;
using Godot;

namespace Project_Star.Core.States;

// 状态容器基类（核心层）：字典驱动的属性存取，供对局资源（AttributeSet）与战斗状态（BattleState）共用。
public abstract class StateContainer
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

    // 深拷贝状态容器（子类须 override 返回自身类型）。
    public virtual StateContainer Clone()
    {
        var copy = (StateContainer)MemberwiseClone();
        copy._values = new Dictionary<StringName, int>(_values);
        return copy;
    }
}
