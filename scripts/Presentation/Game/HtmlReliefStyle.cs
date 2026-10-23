using System.Collections.Generic;
using Godot;

namespace Project_Star.Presentation.Game;

// 当前HTML选角与图鉴的渐变、倒角、阴影和稳定交互状态（表现层）。
internal static class HtmlReliefStyle
{
    private static readonly Dictionary<string, Texture2D> Textures = new();

    // 所有状态共用边距；厚度与按下凹陷只改变绘制，不改变布局。
    internal static StyleBoxTexture Surface(string state = "normal", bool gold = false, int margin = 10)
    {
        var key = $"{state}/{gold}";
        if (!Textures.TryGetValue(key, out var texture))
        {
            var (top, bottom, border) = state switch
            {
                "hover" => ("615345", "493e34", "c3ae8c"),
                "pressed" => ("302820", "473c32", "c3ae8c"),
                "selected" => ("5a4d3c", "40372f", "c3ae8c"),
                "disabled" => ("3b332d", "2f2923", "766b5e"),
                _ => gold ? ("c5af8b", "9e8566", "d8c4a2") : ("50453a", "302923", "ad9676"),
            };
            if (gold && state == "hover") (top, bottom, border) = ("d6c3a4", "af9574", "eedfc4");
            if (gold && state == "pressed") (top, bottom, border) = ("9e8566", "79624e", "d8c4a2");
            using var image = new Image();
            image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='128' height='64'><defs><linearGradient id='g' x2='1' y2='1'><stop stop-color='#{top}'/><stop offset='1' stop-color='#{bottom}'/></linearGradient></defs><rect x='2' y='5' width='124' height='58' rx='7' fill='#000' opacity='.35'/><rect x='1' y='1' width='126' height='59' rx='6' fill='url(#g)' stroke='#{border}' stroke-opacity='.6'/><path d='M8 3H120' stroke='#eee0cd' stroke-opacity='{(state == "pressed" ? ".04" : ".15")}'/><path d='M8 58H120' stroke='#211a16' stroke-width='{(state == "pressed" ? 1 : 2)}' stroke-opacity='.6'/>{(state == "pressed" ? "<path d='M7 4H121' stroke='#000' stroke-width='3' stroke-opacity='.4'/>" : "")}</svg>");
            image.GenerateMipmaps(); texture = ImageTexture.CreateFromImage(image); Textures.Add(key, texture);
        }
        var style = new StyleBoxTexture { Texture = texture, ContentMarginLeft = margin, ContentMarginRight = margin,
            ContentMarginTop = margin, ContentMarginBottom = margin };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetTextureMargin(side, 10);
        return style;
    }

    // 复用HTML细线图标，保留图标按钮的提示与键盘名称。
    internal static Texture2D Icon(StringName key)
    {
        var name = "icon/" + key;
        if (Textures.TryGetValue(name, out var texture)) return texture;
        var path = key.ToString() switch
        {
            "online" => "M21 12a9 9 0 1 0-18 0 9 9 0 1 0 18 0M3 12h18M12 3c-5 5-5 13 0 18 5-5 5-13 0-18Z",
            "hero" => "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8M4 21c0-5 3-7 8-7s8 2 8 7",
            "settings" => "m9 3-1 3-3 1-2 4 2 2v4l3 1 2 3h4l2-3 3-1v-4l2-2-2-4-3-1-1-3ZM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6",
            "tags" => "M3 4h7l11 10-7 7L3 10ZM7 7h.01",
            "reset" => "M5 8a8 8 0 1 1-1 8M5 3v5h5",
            "all" => "M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z",
            "neutral" => "M12 3 3 8v8l9 5 9-5V8ZM12 3v18M3 8l9 5 9-5",
            _ => "M6 6l12 12M18 6 6 18",
        };
        using var image = new Image();
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32' viewBox='0 0 24 24'><path d='{path}' fill='none' stroke='#e2d4bf' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/></svg>");
        image.GenerateMipmaps(); texture = ImageTexture.CreateFromImage(image); Textures.Add(name, texture); return texture;
    }

    // 浮层外阴影随面板绘制，不为阴影增加布局占位。
    internal static void Shadow(Control panel)
    {
        var shadow = new StyleBoxFlat { BgColor = Colors.Transparent, DrawCenter = false,
            ShadowColor = new Color(0, 0, 0, .55f), ShadowSize = 20, ShadowOffset = new Vector2(0, 12) };
        shadow.SetCornerRadiusAll(8);
        panel.Draw += () => panel.DrawStyleBox(shadow, new Rect2(Vector2.Zero, panel.Size));
        panel.Resized += panel.QueueRedraw;
    }

    // 状态与140毫秒亮度过渡对应HTML，不缩放或移动按钮内容。
    internal static void Button(Button button, bool gold = false)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "hover_pressed" })
            button.AddThemeStyleboxOverride(state, Surface(state == "hover_pressed" ? "selected" : state, gold));
        button.AddThemeStyleboxOverride("focus", MatchTheme.Outline(new Color("cbb7a1")));
        button.AddThemeConstantOverride("outline_size", 0);
        button.AddThemeConstantOverride("h_separation", 10);
        Tween? tween = null;
        void Fade(float brightness)
        {
            tween?.Kill(); tween = button.CreateTween();
            tween.TweenProperty(button, "self_modulate", new Color(brightness, brightness, brightness), .14);
        }
        button.MouseEntered += () => Fade(1.06f);
        button.MouseExited += () => Fade(1);
        button.ButtonDown += () => Fade(.94f);
        button.ButtonUp += () => Fade(button.IsHovered() ? 1.06f : 1);
    }
}
