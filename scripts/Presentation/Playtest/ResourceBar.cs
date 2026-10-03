using Godot;

namespace Project_Star.Presentation.Playtest;

// 资源条仅展示快照数值，同时保留文字以免依赖颜色辨认。
public sealed partial class ResourceBar : ProgressBar
{
    private readonly Label _caption = new() { MouseFilter = MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public ResourceBar(string name, Color color)
    {
        Name = name;
        ShowPercentage = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, 24);
        AddThemeStyleboxOverride("background", MatchTheme.Surface(new Color("edf1f3"), new Color("a5bcc9")));
        AddThemeStyleboxOverride("fill", MatchTheme.Surface(color, color));
        _caption.AddThemeFontSizeOverride("font_size", 14);
        _caption.AddThemeColorOverride("font_color", new Color("172b3b"));
        AddChild(_caption);
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    // 不改变实际资源，只更新填充比例和当前值／最大值文字。
    public void Render(string label, int current, int maximum)
    {
        MaxValue = System.Math.Max(1, maximum);
        Value = current;
        _caption.Text = $"{label} {current}/{maximum}";
    }
}
