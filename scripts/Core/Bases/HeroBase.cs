using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

public abstract partial class HeroBase : RefCounted, IEntity
{
    public HeroAttributeSet Attributes { get; private set; }
    public HeroBattleState BattleState { get; private set; }

    protected HeroBase(HeroAttributeSet attributes)
    {
        Attributes = attributes;
        BattleState = new HeroBattleState();
    }

    public object Clone()
    {
        var copy = (HeroBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (HeroAttributeSet)Attributes.Clone();
        copy.BattleState = (HeroBattleState)BattleState.Clone();
        return copy;
    }
}