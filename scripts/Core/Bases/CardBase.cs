using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

// 卡牌实体抽象基类（全局层）：持卡牌对局资源与战斗状态，身份字段经属性集只读转发。
public abstract partial class CardBase : RefCounted, IEntity
{
    // 卡牌对局资源（身份字段 get-only + 等级/价值/对局加成/解锁记录）
    public CardAttributeSet Attributes { get; private set; }
    // 卡牌战斗内临时状态（战斗时派生）
    public CardBattleState BattleState { get; private set; }

    protected CardBase(CardAttributeSet attributes)
    {
        Attributes = attributes;
        BattleState = new CardBattleState();
    }

    // 复制独立卡牌实例（供模板池调用，含独立属性集与战斗状态）。
    public virtual object Clone()
    {
        var copy = (CardBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (CardAttributeSet)Attributes.Clone();
        copy.BattleState = (CardBattleState)BattleState.Clone();
        return copy;
    }
}