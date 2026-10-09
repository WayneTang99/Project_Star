using Godot;
using System.Collections.Generic;

namespace Project_Star.Presentation.Playtest;

// 表现层集中定义深绿桌面、金属边框与紧凑交互样式，不参与玩法状态。
internal static class MatchTheme
{
    public static readonly Color Background = new("0c1918");
    public static readonly Color Ink = new("edf0dd");
    public static readonly Color Gold = new("d9b777");
    public static readonly Color Blue = new("85d8da");
    public static readonly Color SurfaceColor = new("182b24");
    public static readonly Color Muted = new("aeb4a3");
    public const int Gap = 8;
    private static readonly Dictionary<StringName, Texture2D> Icons = new();
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
        row.AddChild(new TextureRect { Texture = Icon(icon), CustomMinimumSize = new Vector2(18, 18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 16); row.AddChild(label);
        return row;
    }

    // 双层金属边线限制在边缘，深色面板不遮挡数字和插画。
    public static void DrawSurface(Control canvas, Rect2 rect)
    {
        canvas.DrawStyleBox(Surface(SurfaceColor, new Color("776345")), rect);
        canvas.DrawRect(rect.Grow(-4), new Color(Gold, .18f), false, 1);
        foreach (var corner in new[] { Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One })
        {
            var point = rect.Position + rect.Size * corner;
            var direction = Vector2.One - corner * 2;
            var start = point + direction * 4;
            canvas.DrawLine(start, start + new Vector2(direction.X * 12, 0), new Color(Gold, .5f), 1, true);
            canvas.DrawLine(start, start + new Vector2(0, direction.Y * 12), new Color(Gold, .5f), 1, true);
        }
    }

    // 背景星图作为低对比纹理，不承载交互或玩法信息。
    public static void DrawBackground(Control canvas)
    {
        _stars ??= GD.Load<Texture2D>("res://art/ui/theme/stars.svg");
        canvas.DrawTextureRect(_stars, new Rect2(Vector2.Zero, canvas.Size), true, new Color(Gold, .13f));
    }

    // 每个界面拥有独立Theme，卡面仍使用自身的原画和数值样式。
    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("default_color", "RichTextLabel", Ink);
        theme.SetConstant("separation", "VBoxContainer", Gap);
        theme.SetConstant("separation", "HBoxContainer", Gap);
        theme.SetConstant("h_separation", "HFlowContainer", Gap);
        theme.SetConstant("v_separation", "HFlowContainer", Gap);
        theme.SetStylebox("panel", "PanelContainer", Surface(SurfaceColor, new Color("776345")));
        theme.SetStylebox("panel", "TooltipPanel", Surface(new Color("17251f"), Gold));
        theme.SetColor("font_color", "TooltipLabel", Ink);
        theme.SetStylebox("normal", "Button", Surface(new Color("233a2d"), new Color("756847")));
        theme.SetStylebox("hover", "Button", Surface(new Color("354c38"), Gold));
        theme.SetStylebox("pressed", "Button", Surface(new Color("172b23"), Blue));
        theme.SetStylebox("disabled", "Button", Surface(new Color("1c2b25"), new Color("434d3c")));
        theme.SetStylebox("focus", "Button", Outline(Blue));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            theme.SetColor(state, "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("7c8c7d"));
        theme.SetStylebox("panel", "PopupMenu", Surface(SurfaceColor, Gold));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetStylebox("normal", "LineEdit", Surface(new Color("10211b"), new Color("756847")));
        theme.SetStylebox("focus", "LineEdit", Outline(Blue));
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetColor("font_placeholder_color", "LineEdit", Muted);
        theme.SetStylebox("normal", "OptionButton", Surface(SurfaceColor, Gold));
        theme.SetStylebox("hover", "OptionButton", Surface(new Color("354c38"), Gold));
        theme.SetStylebox("pressed", "OptionButton", Surface(SurfaceColor, Blue));
        theme.SetColor("font_color", "OptionButton", Ink);
        theme.SetColor("font_hover_color", "OptionButton", Ink);
        theme.SetColor("font_pressed_color", "OptionButton", Ink);
        return theme;
    }

    // 通用面板边距和描边由Theme共享，浮层不再各自硬编码深色样式。
    public static StyleBoxFlat Surface(Color fill, Color border)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border,
            ContentMarginLeft = Gap, ContentMarginRight = Gap, ContentMarginTop = 5, ContentMarginBottom = 5 };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(3);
        return style;
    }

    // 主要行动与交易按钮共用金色底板，禁用状态继续由快照决定。
    public static void Accent(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Surface(new Color("79582c"), Gold));
        button.AddThemeStyleboxOverride("hover", Surface(new Color("a27c40"), new Color("ffedb3")));
        button.AddThemeStyleboxOverride("pressed", Surface(new Color("624521"), Gold));
        button.AddThemeColorOverride("font_color", new Color("fff0c4"));
    }

    // 选中和键盘焦点共享清晰的空心描边。
    public static StyleBoxFlat Outline(Color color)
    {
        var style = Surface(Colors.Transparent, color); style.SetBorderWidthAll(3); return style;
    }
}
