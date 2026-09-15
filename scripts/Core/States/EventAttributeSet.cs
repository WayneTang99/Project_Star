using Godot;

namespace Project_Star.Core.States;

public class EventAttributeSet : AttributeSet
{
    public StringName EventKey { get; }
    public string DisplayName { get; }

    public EventAttributeSet(StringName eventKey, string displayName)
    {
        EventKey = eventKey;
        DisplayName = displayName;
    }
}