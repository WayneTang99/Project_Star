using Godot;

namespace Project_Star.Presentation.Playtest;

// 表现层集中定义白金面板、天蓝交互与紧凑字号，不参与玩法状态。
internal static class MatchTheme
{
    public static readonly Color Background = new("dce9ef");
    public static readonly Color Ink = new("243d52");
    public static readonly Color Gold = new("bc9144");
    public static readonly Color Blue = new("4386b0");
    public const int Gap = 8;

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
