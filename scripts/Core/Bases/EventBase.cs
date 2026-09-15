using System;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Core.States;

namespace Project_Star.Core.Bases;

public abstract partial class EventBase : RefCounted, IEntity
{
    public EventAttributeSet Attributes { get; private set; }

    protected EventBase(EventAttributeSet attributes)
    {
        Attributes = attributes;
    }

    public object Clone()
    {
        var copy = (EventBase)Activator.CreateInstance(GetType())!;
        copy.Attributes = (EventAttributeSet)Attributes.Clone();
        return copy;
    }
}