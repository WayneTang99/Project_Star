using Godot;

namespace Project_Star.Presentation.Playtest;

// 资源条仅展示快照数值，同时保留文字以免依赖颜色辨认。
public sealed partial class ResourceBar : ProgressBar
{
    private readonly Texture2D _fillTexture;
    private readonly Label _caption = new() { MouseFilter = MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextureRect _icon = new() { MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };

    public ResourceBar(string name, Color color)
    {
        Name = name;
        _fillTexture = CreateFill(name.Contains("Mana")).Texture;
        ShowPercentage = false;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, 24);
        AddThemeStyleboxOverride("background", MatchTheme.Plate("resource-frame", 4));
        AddThemeStyleboxOverride("fill", new StyleBoxEmpty());
        _caption.AddThemeFontSizeOverride("font_size", 14);
        _caption.AddThemeFontOverride("font", MatchTheme.Font(true, true));
        _caption.AddThemeColorOverride("font_color", new Color("fff2cd"));
        AddChild(_caption);
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _caption.OffsetLeft = 18;
        AddChild(_icon); _icon.Position = new Vector2(6, 4); _icon.Size = new Vector2(16, 16);
    }

    private static StyleBoxTexture CreateFill(bool mana)
    {
        var resource = new Color(mana ? "cc99ff" : "55d6a5");
        var dark = new Color("11151d");
        return new StyleBoxTexture { Texture = new GradientTexture2D { Width = 244, Height = 24,
            FillFrom = new Vector2(0, 1), FillTo = Vector2.Zero,
            Gradient = new Gradient { Colors = new[] { dark.Lerp(resource, .18f), dark.Lerp(resource, .32f) } } } };
    }

    public override void _Draw()
    {
        var width = Mathf.Max(0, Size.X - 8) * (float)(Value / System.Math.Max(1, MaxValue));
        if (width > 0) DrawTextureRect(_fillTexture, new Rect2(4, 4, width, Mathf.Max(1, Size.Y - 8)), false);
    }

    // 底部横向HUD使用紧凑资源条，数值与悬停语义保持完整。
    public void SetDisplayHeight(float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        _caption.AddThemeFontSizeOverride("font_size", height <= 10 ? 9 : height >= 24 ? 14 : 12);
        _caption.OffsetLeft = 0;
        _icon.Visible = height >= 24;
        _caption.OffsetLeft = 0;
        _icon.Position = new Vector2(-27, 1); _icon.Size = new Vector2(20, 20);
    }

    // 不改变实际资源，只更新填充比例和当前值／最大值文字。
    public void Render(string label, int current, int maximum)
    {
        MaxValue = System.Math.Max(1, maximum);
        Value = current;
        _caption.Text = $"{current} / {maximum}";
        _icon.Texture = MatchTheme.Icon(label == "生命" ? new StringName("health")
            : label == "魔法" ? new StringName("mana") : new StringName("experience"));
        if (AttributePalette.Find(label == "生命" ? AttributePalette.Health : label == "魔法"
            ? Project_Star.Domain.Common.GameAttributeKeys.Mana : Project_Star.Domain.Common.GameAttributeKeys.Experience) is { } color)
        {
            _caption.AddThemeColorOverride("font_color", color);
            _caption.AddThemeColorOverride("font_outline_color", new Color("071513"));
            _caption.AddThemeConstantOverride("outline_size", 1);
            _icon.Modulate = color; _icon.Material = AttributePalette.IconMaterial;
        }
        TooltipText = $"{label} {current}/{maximum}";
    }
}
