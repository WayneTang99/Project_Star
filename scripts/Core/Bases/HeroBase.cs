using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

// 英雄实体抽象基类（全局层）：持英雄对局资源与战斗状态，身份字段经属性集只读转发。
public abstract partial class HeroBase : RefCounted, IEntity
{
    // 英雄对局资源（身份字段 get-only + 金钱/经验/等级/声望）
    public HeroAttributeSet Attributes { get; private set; }
    // 英雄战斗内临时状态（战斗时派生）
    public HeroBattleState BattleState { get; private set; }

    protected HeroBase(HeroAttributeSet attributes)
    {
        Attributes = attributes;
        BattleState = new HeroBattleState();
    }

    // 复制独立英雄实例（供模板池调用，含独立属性集与战斗状态）。
    public object Clone()
    {
        var copy = (HeroBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (HeroAttributeSet)Attributes.Clone();
        copy.BattleState = (HeroBattleState)BattleState.Clone();
        return copy;
    }
}