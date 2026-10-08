using Godot;
using System.Collections.Generic;

namespace Project_Star.Presentation.Playtest;

// 表现层集中定义白金面板、天蓝交互与紧凑字号，不参与玩法状态。
internal static class MatchTheme
{
    public static readonly Color Background = new("dce9ef");
    public static readonly Color Ink = new("243d52");
    public static readonly Color Gold = new("bc9144");
    public static readonly Color Blue = new("4386b0");
    public const int Gap = 8;
    private static readonly Dictionary<StringName, Texture2D> Icons = new();
    private static Texture2D? _paper;
    private static Texture2D? _stars;

    // 复用卡面语义图标，其余界面图标使用同一套细线纹样。
    public static Texture2D Icon(StringName key)
    {
        if (Icons.TryGetValue(key, out var texture)) return texture;
        var path = key == new StringName("health") ? "res://art/ui/card-face/icons/healing.svg"
            : key == new StringName("mana") ? "res://art/ui/card-face/icons/mana.svg"
            : key == new StringName("armor") ? "res://art/ui/card-face/icons/armor.svg"
            : key == new StringName("burn") ? "res://art/ui/card-face/icons/burn.svg"
            : key == new StringName("poison") ? "res://art/ui/card-face/icons/poison.svg"
            : key == new StringName("coin") ? "res://art/ui/card-face/icons/value.svg"
            : $"res://art/ui/theme/{key}.svg";
        texture = GD.Load<Texture2D>(path); Icons.Add(key, texture); return texture;
    }

    // 数值与图标并排显示，完整语义保留在悬停提示。
    public static HBoxContainer Stat(StringName icon, string value, string tooltip)
    {
        var row = new HBoxContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(new TextureRect { Texture = Icon(icon), CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 13); row.AddChild(label);
        return row;
    }

    // 纸纹只铺面板底色，角花限制在边缘，不遮挡数字和插画。
    public static void DrawSurface(Control canvas, Rect2 rect)
    {
        canvas.DrawStyleBox(Surface(new Color("fffaf0"), Gold), rect);
        _paper ??= GD.Load<Texture2D>("res://art/ui/theme/paper.svg");
        canvas.DrawTextureRect(_paper, rect.Grow(-2), true, new Color(1, 1, 1, .3f));
        foreach (var corner in new[] { Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One })
        {
            var point = rect.Position + rect.Size * corner;
            var direction = Vector2.One - corner * 2;
            var start = point + direction * 4;
            canvas.DrawLine(start, start + new Vector2(direction.X * 18, 0), Gold, 1, true);
            canvas.DrawLine(start, start + new Vector2(0, direction.Y * 18), Gold, 1, true);
            var diamond = point + direction * 10;
            canvas.DrawPolyline(new[] { diamond + Vector2.Up * 3, diamond + Vector2.Right * 3,
                diamond + Vector2.Down * 3, diamond + Vector2.Left * 3, diamond + Vector2.Up * 3 },
                new Color(Gold, .65f), 1, true);
        }
    }

    // 背景星图作为低对比纹理，不承载交互或玩法信息。
    public static void DrawBackground(Control canvas)
    {
        _stars ??= GD.Load<Texture2D>("res://art/ui/theme/stars.svg");
        canvas.DrawTextureRect(_stars, new Rect2(Vector2.Zero, canvas.Size), true, new Color(1, 1, 1, .45f));
    }

    // 每个界面拥有独立Theme，卡面仍使用自身的原画和数值样式。
    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("default_color", "RichTextLabel", Ink);
        theme.SetConstant("separation", "VBoxContainer", Gap);
        theme.SetConstant("separation", "HBoxContainer", Gap);
        theme.SetConstant("h_separation", "HFlowContainer", Gap);
        theme.SetConstant("v_separation", "HFlowContainer", Gap);
        theme.SetStylebox("panel", "PanelContainer", Surface(new Color("fffaf0"), Gold));
        theme.SetStylebox("panel", "TooltipPanel", Surface(new Color("fffaf0"), Gold));
        theme.SetColor("font_color", "TooltipLabel", Ink);
        theme.SetStylebox("normal", "Button", Surface(new Color("f8fbfc"), new Color("a5bcc9")));
        theme.SetStylebox("hover", "Button", Surface(new Color("e1f2fb"), Blue));
        theme.SetStylebox("pressed", "Button", Surface(new Color("bddcec"), Blue));
        theme.SetStylebox("disabled", "Button", Surface(new Color("e4ebed"), new Color("c1cdd1")));
        theme.SetStylebox("focus", "Button", Outline(Blue));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            theme.SetColor(state, "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("6e818b"));
        theme.SetStylebox("panel", "PopupMenu", Surface(new Color("fffaf0"), Gold));
        theme.SetColor("font_color", "PopupMenu", Ink);
        return theme;
    }

    // 通用面板边距和描边由Theme共享，浮层不再各自硬编码深色样式。
    public static StyleBoxFlat Surface(Color fill, Color border)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border,
            ContentMarginLeft = Gap, ContentMarginRight = Gap, ContentMarginTop = 5, ContentMarginBottom = 5 };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(6);
        return style;
    }

    // 选中和键盘焦点共享清晰的空心描边。
    public static StyleBoxFlat Outline(Color color)
    {
        var style = Surface(Colors.Transparent, color); style.SetBorderWidthAll(3); return style;
    }
}
