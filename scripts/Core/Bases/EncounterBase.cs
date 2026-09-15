using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

// 遭遇实体抽象基类（对局层）：持遭遇对局资源，身份字段经属性集只读转发。
// 遭遇 = 玩家回合遭遇选项（商店/怪物战/PvP），与总线事件（MatchEvent/CombatEvent）区分。
public abstract partial class EncounterBase : RefCounted, IEntity
{
    // 遭遇对局资源（身份字段 get-only）
    public EncounterAttributeSet Attributes { get; private set; }

    protected EncounterBase(EncounterAttributeSet attributes)
    {
        Attributes = attributes;
    }

    // 复制独立遭遇实例（供模板池调用，含独立属性集）。
    public object Clone()
    {
        var copy = (EncounterBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (EncounterAttributeSet)Attributes.Clone();
        return copy;
    }
}