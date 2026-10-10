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
    private TextureRect _art = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    public override void _Ready()
    {
        _message = new RichTextLabel { Name = "Message" }; AddChild(_message);
        _art = new TextureRect { Name = "Illustration", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true }; AddChild(_art); _art.Hide();
        _scroll = new ScrollContainer { Name = "ActionScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _actions = new VBoxContainer { Name = "Actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_actions);
        Resized += LayoutView; LayoutView();
    }

    // 快照版本随按钮绑定；过期选项交给协调器拒绝。
    public void Render(string message, IReadOnlyList<KeyedAction> actions, long revision = 0, StringName? illustration = null, int level = 0)
    {
        Clear(); _message.Text = message;
        var path = illustration?.ToString() ?? "";
        _art.Texture = path.Length > 0 && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _art.Visible = _art.Texture is not null;
        LevelPresentation.Frame(_art, level);
        foreach (var action in actions)
        {
            var button = new Button { Name = $"Action{_actions.GetChildCount()}", Text = action.Action.Text
                    + (action.Subtitle.Length > 0 ? "\n" + action.Subtitle : ""),
                Visible = action.Action.Visible, Disabled = !action.Action.Enabled, ClipText = true,
                CustomMinimumSize = new Vector2(0, 56),
                TooltipText = action.Action.Text + "\n" + action.Subtitle + "\n" + action.Action.Reason };
            if (action.Subtitle.Contains('\n')) button.AddThemeFontSizeOverride("font_size", 16);
            Action handler = () => { if (!button.Disabled && button.Visible) Selected?.Invoke(action.Key, revision); };
            _actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
            LevelPresentation.Frame(button, action.Level);
        }
        LayoutView();
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
        var inset = _art.Visible ? Size.X * .44f : 0;
        _art.Position = Vector2.Zero; _art.Size = new Vector2(Mathf.Max(1, inset - 16), Size.Y);
        _message.Position = new Vector2(inset, 0); _message.Size = new Vector2(Size.X - inset, 28);
        _scroll.Position = new Vector2(inset, 32); _scroll.Size = new Vector2(Size.X - inset, Mathf.Max(1, Size.Y - 32));
    }
}
