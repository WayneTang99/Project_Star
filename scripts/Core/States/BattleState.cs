using System.Collections.Generic;
using Godot;

namespace Project_Star.Core.States;

// 战斗内临时状态容器（战斗层）：每场战斗开始派生初始值、结束丢弃，数值以 StringName 为 key 存入字典。
public abstract class BattleState
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

    // 深拷贝战斗状态（供战斗初始值派生时调用）。
    public virtual BattleState Clone()
    {
        var copy = (BattleState)MemberwiseClone();
        copy._values = new Dictionary<StringName, int>(_values);
        return copy;
    }
}