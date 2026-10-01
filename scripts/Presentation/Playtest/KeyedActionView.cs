using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 英雄、遭遇、事件共用说明和选项布局；事件保留快照版本。
public sealed partial class KeyedActionView : Control
{
    public event Action<StringName, long>? Selected;
    private RichTextLabel _message = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _actions = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    public override void _Ready()
    {
        _message = new RichTextLabel { Name = "Message" }; AddChild(_message);
        _scroll = new ScrollContainer { Name = "ActionScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _actions = new VBoxContainer { Name = "Actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_actions);
        Resized += LayoutView; LayoutView();
    }

    // 快照版本随按钮绑定；过期选项交给协调器拒绝。
    public void Render(string message, IReadOnlyList<KeyedAction> actions, long revision = 0)
    {
        Clear(); _message.Text = message;
        foreach (var action in actions)
        {
            var button = new Button { Name = $"Action{_actions.GetChildCount()}", Text = action.Action.Text,
                Visible = action.Action.Visible, Disabled = !action.Action.Enabled, ClipText = true,
                TooltipText = action.Action.Text + "\n" + action.Action.Reason };
            Action handler = () => { if (!button.Disabled && button.Visible) Selected?.Invoke(action.Key, revision); };
            _actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
        }
    }

    public override void _ExitTree() { Resized -= LayoutView; Clear(); }

    private void Clear()
    {
        foreach (var (button, handler) in _bindings)
        { button.Pressed -= handler; _actions.RemoveChild(button); button.QueueFree(); }
        _bindings.Clear();
    }

    private void LayoutView()
    {
        _message.Size = new Vector2(Size.X, 50);
        _scroll.Position = new Vector2(0, 54); _scroll.Size = new Vector2(Size.X, Mathf.Max(1, Size.Y - 54));
    }
}
