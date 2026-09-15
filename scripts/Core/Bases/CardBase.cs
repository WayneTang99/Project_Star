using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

public abstract partial class CardBase : RefCounted, IEntity
{
    public CardAttributeSet Attributes { get; private set; }
    public CardBattleState BattleState { get; private set; }

    protected CardBase(CardAttributeSet attributes)
    {
        Attributes = attributes;
        BattleState = new CardBattleState();
    }

    public object Clone()
    {
        var copy = (CardBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (CardAttributeSet)Attributes.Clone();
        copy.BattleState = (CardBattleState)BattleState.Clone();
        return copy;
    }
}