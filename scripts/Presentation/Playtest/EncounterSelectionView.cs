using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 遭遇图卡选择视图（表现层）；只展示快照并提交选择意图。
public sealed partial class EncounterSelectionView : Control
{
    public event Action<StringName, long>? Selected;
    private Label _message = null!;
    private HBoxContainer _choices = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();

    public override void _Ready()
    {
        _message = new Label { Name = "Message", TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        AddChild(_message);
        _choices = new HBoxContainer { Name = "Choices", Alignment = BoxContainer.AlignmentMode.Center }; _choices.AddThemeConstantOverride("separation", 40); AddChild(_choices);
        Resized += LayoutView; LayoutView();
    }

    // 整张图卡可点击或键盘选择，装饰层不拦截输入。
    public void Render(string message, IReadOnlyList<KeyedAction> choices)
    {
        Clear(); _message.Text = message;
        foreach (var choice in choices)
        {
            var button = new Button { Name = $"Choice{_bindings.Count}", Visible = choice.Action.Visible,
                Disabled = !choice.Action.Enabled,
                ClipContents = true, TooltipText = choice.Action.Text + "\n" + choice.Action.Reason
                    + (choice.Level > 0 ? $"\n遭遇等级 {choice.Level}" : "")
                    + (choice.ShopLevel > 0 ? $"\n{choice.ShopLevel}级商店 · 商品等级不高于{choice.ShopLevel}级" : "") };
            _choices.AddChild(button);
            var art = new TextureRect { Name = "Illustration", MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
            if (!choice.Illustration.IsEmpty && ResourceLoader.Exists(choice.Illustration.ToString()))
                art.Texture = GD.Load<Texture2D>(choice.Illustration.ToString());
            button.AddChild(art); art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            art.OffsetLeft = art.OffsetTop = 3; art.OffsetRight = -3; art.OffsetBottom = -3;
            var shade = new TextureRect { Texture = GD.Load<Texture2D>("res://art/ui/html/encounter-shade.svg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var label = new Label { Name = "Title", Text = choice.Action.Text, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            MatchTheme.Text(label, 17, new Color("eddbb3"), spacing: 2);
            button.AddChild(label); label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            label.OffsetLeft = 17; label.OffsetRight = -17; label.OffsetTop = -61; label.OffsetBottom = -33;
            var subtitle = new Label { Name = "Subtitle", Text = choice.Subtitle,
                MouseFilter = MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            MatchTheme.Text(subtitle, 10, new Color("b7c7b6"));
            button.AddChild(subtitle); subtitle.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            subtitle.OffsetLeft = 17; subtitle.OffsetRight = -17; subtitle.OffsetTop = -32; subtitle.OffsetBottom = -15;
            if (choice.Level > 0)
            {
                var badge = new Control { Position = new Vector2(7, 7), Size = new Vector2(92, 26), MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(badge);
                var crystal = new CardLevelGem { Name = "EncounterLevelCrystal", Position = new Vector2(3, 1),
                    Size = new Vector2(20, 24), MouseFilter = MouseFilterEnum.Ignore };
                badge.AddChild(crystal); crystal.SetLevel(choice.Level);
                var level = new Label { Name = "EncounterLevel", Text = choice.ShopLevel > 0 ? $"{choice.Level}级商店" : $"等级 {choice.Level}", Position = new Vector2(27, 0),
                    Size = new Vector2(62, 26), MouseFilter = MouseFilterEnum.Ignore };
                level.AddThemeColorOverride("font_color", Colors.White); badge.AddChild(level);
                MatchTheme.Text(level, 10, MatchTheme.Gold);
            }
            // 焦点与悬停边框绘制在原画之上。
            var outline = new Panel { Name = "Outline", MouseFilter = MouseFilterEnum.Ignore };
            outline.AddThemeStyleboxOverride("panel", MatchTheme.Outline(MatchTheme.Blue));
            button.AddChild(outline); outline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); outline.Hide();
            button.MouseEntered += outline.Show;
            button.MouseExited += () => { if (!button.HasFocus()) outline.Hide(); };
            button.FocusEntered += outline.Show;
            button.FocusExited += () => { if (!button.IsHovered()) outline.Hide(); };
            Action handler = () => { if (!button.Disabled && button.Visible) Selected?.Invoke(choice.Key, 0); };
            button.Pressed += handler; _bindings.Add((button, handler));
        }
        LayoutView();
    }

    public override void _ExitTree() { Resized -= LayoutView; Clear(); }

    private void Clear()
    {
        foreach (var (button, handler) in _bindings)
        { button.Pressed -= handler; _choices.RemoveChild(button); button.QueueFree(); }
        _bindings.Clear();
    }

    private void LayoutView()
    {
        _message.Hide();
        var height = (Size.Y - 32) * .85f;
        _choices.Position = new Vector2(22, (Size.Y - height) / 2);
        _choices.Size = new Vector2(Size.X - 44, height);
        foreach (var (button, _) in _bindings)
            button.CustomMinimumSize = new Vector2(Mathf.Min(260, (Size.X - 44 - Math.Max(0, _bindings.Count - 1) * 40) / Math.Max(1, _bindings.Count)), height);
    }
}
