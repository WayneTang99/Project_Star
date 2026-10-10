using System;
using Godot;
using System.Collections.Generic;

namespace Project_Star.Presentation.Playtest;

// 表现层集中定义深绿桌面、金属边框与紧凑交互样式，不参与玩法状态。
internal static class MatchTheme
{
    public static readonly Color Background = new("0c1918");
    public static readonly Color Ink = new("edf0dd");
    public static Color Gold => new(BluePalette ? "94c5d1" : "d9b777");
    public static Color Blue => new(BluePalette ? "a9e5ef" : "85d8da");
    public static readonly Color SurfaceColor = new("182b24");
    public static readonly Color Muted = new("aeb4a3");
    public const int Gap = 8;
    private static readonly Dictionary<StringName, Texture2D> Icons = new();
    private static Texture2D? _stars;
    private static Vector2 _backgroundSize;
    private static readonly Dictionary<string, Godot.Font> Fonts = new();
    private static readonly Dictionary<string, Texture2D> Panels = new();
    public static bool BluePalette { get; private set; }

    // 与 HTML 共用系统字体，按控件角色缓存字重与字距。
    public static Godot.Font Font(bool serif = false, bool bold = false, int spacing = 0)
    {
        var key = $"{serif}/{bold}/{spacing}";
        if (Fonts.TryGetValue(key, out var font)) return font;
        font = new FontVariation { BaseFont = new SystemFont { FontNames = new[] { serif ? "Georgia" : "Microsoft YaHei" }, FontWeight = bold ? 700 : 400 }, SpacingGlyph = spacing };
        Fonts.Add(key, font); return font;
    }

    // 配色切换只作用于表现，不改变对局数据。
    public static void SetPalette(bool blue) => BluePalette = blue;

    // 从 HTML 提取的渐变和内外描边随面板实际尺寸绘制。
    public static StyleBoxTexture Plate(string name, float margin = 4)
    {
        var style = new StyleBoxTexture { Texture = GD.Load<Texture2D>($"res://art/ui/html/{name}.svg"),
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5 };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetTextureMargin(side, margin);
        return style;
    }

    // 每种文字使用明确的字体、字号、字重与字距。
    public static void Text(Label label, int size, Color? color = null, bool serif = false, bool bold = false, int spacing = 0)
    {
        label.AddThemeFontOverride("font", Font(serif, bold, spacing));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        label.AddThemeConstantOverride("outline_size", 0);
    }

    // 复用卡面语义图标，其余界面图标使用同一套细线纹样。
    public static Texture2D Icon(StringName key)
    {
        if (Icons.TryGetValue(key, out var texture)) return texture;
        var htmlKey = key == new StringName("health") ? new StringName("healing") : key;
        var path = ResourceLoader.Exists($"res://art/ui/html/{htmlKey}.svg") ? $"res://art/ui/html/{htmlKey}.svg"
            : key == new StringName("health") ? "res://art/ui/card-face/icons/healing.svg"
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
        row.AddChild(new TextureRect { Texture = Icon(icon), CustomMinimumSize = new Vector2(12, 12),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        Text(label, 11, Muted); row.AddChild(label);
        return row;
    }

    // 双层金属边线限制在边缘，深色面板不遮挡数字和插画。
    public static void DrawSurface(Control canvas, Rect2 rect, string kind = "surface")
    {
        var shadow = Surface(Colors.Transparent, Colors.Transparent);
        shadow.ShadowColor = new Color(0, 0, 0, kind == "tooltip" ? .667f : .25f); shadow.ShadowSize = kind == "tooltip" ? 22 : 10; shadow.ShadowOffset = new Vector2(0, kind == "tooltip" ? 14 : 8);
        canvas.DrawStyleBox(shadow, rect);
        var width = Mathf.Max(1, Mathf.RoundToInt(rect.Size.X)); var height = Mathf.Max(1, Mathf.RoundToInt(rect.Size.Y));
        var key = $"{kind}/{BluePalette}/{width}/{height}";
        if (!Panels.TryGetValue(key, out var texture))
        {
            var angle = Mathf.DegToRad(kind == "tooltip" ? 130 : BluePalette ? 125 : kind == "stage" ? 120 : kind == "hero" ? 100 : 150);
            var direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            var length = Mathf.Abs(width * direction.X) + Mathf.Abs(height * direction.Y);
            var start = rect.Size / 2 - direction * length / 2; var end = rect.Size / 2 + direction * length / 2;
            var stops = kind == "tooltip" ? "<stop stop-color='#282a22'/><stop offset='1' stop-color='#131f1b'/>"
                : BluePalette ? "<stop stop-color='#293c46'/><stop offset='1' stop-color='#172b34'/>"
                : kind == "stage" ? "<stop stop-color='#1d322d'/><stop offset='.65' stop-color='#142622'/><stop offset='1' stop-color='#23342c'/>"
                : kind == "hero" ? "<stop stop-color='#1c2b25'/><stop offset='1' stop-color='#17251f'/>"
                : "<stop stop-color='#253a2c'/><stop offset='1' stop-color='#182b24'/>";
            using var image = new Image();
            image.LoadSvgFromString(FormattableString.Invariant($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'><defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x1='{start.X}' y1='{start.Y}' x2='{end.X}' y2='{end.Y}'>{stops}</linearGradient></defs><rect width='100%' height='100%' fill='url(#g)'/></svg>"));
            texture = ImageTexture.CreateFromImage(image); Panels.Add(key, texture);
        }
        canvas.DrawTextureRect(texture, rect, false);
        var border = Surface(Colors.Transparent, new Color(kind == "tooltip" ? "c0a269" : BluePalette ? "698d98" : kind == "stage" ? "7f744b" : kind == "hero" ? "716444" : "968358")); border.SetCornerRadiusAll(0);
        canvas.DrawStyleBox(border, rect);
        var inset = Surface(Colors.Transparent, new Color(kind == "tooltip" ? "141a15" : kind == "stage" ? "0c1c19" : kind == "hero" ? "0c1916" : "0b1915")); inset.SetBorderWidthAll(kind == "tooltip" ? 4 : 3); inset.SetCornerRadiusAll(0);
        canvas.DrawStyleBox(inset, rect.Grow(-1));
        if (kind != "hero" && kind != "tooltip") canvas.DrawRect(rect.Grow(kind == "stage" ? -8 : -5), new Color(Gold, kind == "stage" ? .15f : .22f), false, 1);
    }

    // 背景星图作为低对比纹理，不承载交互或玩法信息。
    public static void DrawBackground(Control canvas)
    {
        if (_stars is null || _backgroundSize != canvas.Size) { _backgroundSize = canvas.Size; _stars = CreateBackground(canvas.Size); }
        canvas.DrawRect(new Rect2(Vector2.Zero, canvas.Size), BluePalette ? new Color("101c28") : Background);
        canvas.DrawTextureRect(_stars, new Rect2(Vector2.Zero, canvas.Size), false);
        canvas.DrawSetTransform(new Vector2(-150, canvas.Size.Y * .21f), Mathf.DegToRad(-12));
        canvas.DrawString(Font(true), new Vector2(0, 740), "✧", fontSize: 640, modulate: new Color("bbac7510"));
        canvas.DrawSetTransform(Vector2.Zero);
    }

    private static Texture2D CreateBackground(Vector2 size)
    {
        using var image = new Image();
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='{size.X}' height='{size.Y}'><defs><radialGradient id='a' cx='0' cy='0' r='1' gradientTransform='translate(.5 .1) scale(.38891 .70004)'><stop stop-color='#466044' stop-opacity='.333333'/><stop offset='1' stop-color='#466044' stop-opacity='0'/></radialGradient><radialGradient id='b' cx='0' cy='0' r='1' gradientTransform='translate(0 .8) scale(.636396 .509117)'><stop stop-color='#264942' stop-opacity='.466667'/><stop offset='1' stop-color='#264942' stop-opacity='0'/></radialGradient><pattern id='p' width='7' height='7' patternUnits='userSpaceOnUse' patternTransform='rotate(25)'><rect width='1' height='7' fill='#d5c080' fill-opacity='.011765'/></pattern></defs><rect width='100%' height='100%' fill='url(#p)'/><rect width='100%' height='100%' fill='url(#b)'/><rect width='100%' height='100%' fill='url(#a)'/></svg>");
        return ImageTexture.CreateFromImage(image);
    }

    // 每个界面拥有独立Theme，卡面仍使用自身的原画和数值样式。
    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 14, DefaultFont = Font() };
        theme.SetFont("normal_font", "RichTextLabel", Font());
        theme.SetFont("bold_font", "RichTextLabel", Font(bold: true));
        theme.SetFont("italics_font", "RichTextLabel", Font());
        theme.SetFont("bold_italics_font", "RichTextLabel", Font(bold: true));
        theme.SetFont("font", "TooltipLabel", Font());
        theme.SetFontSize("font_size", "TooltipLabel", 12);
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("default_color", "RichTextLabel", Ink);
        theme.SetConstant("separation", "VBoxContainer", Gap);
        theme.SetConstant("separation", "HBoxContainer", Gap);
        theme.SetConstant("h_separation", "HFlowContainer", Gap);
        theme.SetConstant("v_separation", "HFlowContainer", Gap);
        theme.SetStylebox("panel", "PanelContainer", Plate(BluePalette ? "blue-surface" : "surface"));
        theme.SetStylebox("panel", "TooltipPanel", Plate("tooltip"));
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
        button.AddThemeStyleboxOverride("normal", Plate(BluePalette ? "blue-button" : "gold-button", 4));
        button.AddThemeStyleboxOverride("hover", Plate(BluePalette ? "blue-button" : "gold-button", 4));
        button.AddThemeStyleboxOverride("pressed", Plate(BluePalette ? "blue-button" : "gold-button", 4));
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", new Color("fff0c4"));
    }

    // 选中和键盘焦点共享清晰的空心描边。
    public static StyleBoxFlat Outline(Color color)
    {
        var style = Surface(Colors.Transparent, color); style.SetBorderWidthAll(3); return style;
    }
}
