using Godot;

namespace Project_Star.Presentation.Playtest;

// 资源条仅展示快照数值，同时保留文字以免依赖颜色辨认。
public sealed partial class ResourceBar : ProgressBar
{
    private readonly Label _caption = new() { MouseFilter = MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextureRect _icon = new() { MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };

    public ResourceBar(string name, Color color)
    {
        Name = name;
        ShowPercentage = false;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, 24);
        AddThemeStyleboxOverride("background", MatchTheme.Surface(new Color("0b1713"), new Color("817449")));
        AddThemeStyleboxOverride("fill", MatchTheme.Surface(color, color));
        _caption.AddThemeFontSizeOverride("font_size", 14);
        _caption.AddThemeColorOverride("font_color", new Color("f2f3d9"));
        AddChild(_caption);
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _caption.OffsetLeft = 18;
        AddChild(_icon); _icon.Position = new Vector2(6, 4); _icon.Size = new Vector2(16, 16);
    }

    // 底部横向HUD使用紧凑资源条，数值与悬停语义保持完整。
    public void SetDisplayHeight(float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        _caption.AddThemeFontSizeOverride("font_size", height < 22 ? 14 : 16);
        _caption.OffsetLeft = 0;
        _icon.Hide();
    }

    // 不改变实际资源，只更新填充比例和当前值／最大值文字。
    public void Render(string label, int current, int maximum)
    {
        MaxValue = System.Math.Max(1, maximum);
        Value = current;
        _caption.Text = $"{current}/{maximum}";
        _icon.Texture = MatchTheme.Icon(label == "生命" ? new StringName("health")
            : label == "魔法" ? new StringName("mana") : new StringName("experience"));
        TooltipText = $"{label} {current}/{maximum}";
    }
}
