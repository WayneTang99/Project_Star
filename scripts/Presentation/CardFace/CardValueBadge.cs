using Godot;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.CardFace;

// 持有价值徽标，按卡面实际大小设置文字和图标。
public sealed partial class CardValueBadge : HBoxContainer
{
    private Label _value = null!;

    public override void _Ready()
    {
        _value = GetNode<Label>("Value");
        GetNode<TextureRect>("Icon").Texture = MatchTheme.Icon("coin");
        MatchTheme.Text(_value, 13, new Color("ffe8b5"), true, true);
        _value.AddThemeColorOverride("font_color", new Color("fff2cd"));
        _value.AddThemeColorOverride("font_outline_color", new Color("241810"));
    }

    // 显示快照中的持有价值。
    public void SetValue(int value)
    {
        _value.Text = value.ToString();
    }

    // 紧凑卡面保留可读文字而非整体缩放。
    public void SetDisplayMetrics(int font)
    {
        GetNode<TextureRect>("Icon").CustomMinimumSize = new Vector2(10, 10);
        GetNode<TextureRect>("Icon").SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddThemeConstantOverride("separation", 3);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.HorizontalAlignment = HorizontalAlignment.Left;
        _value.ClipText = false;
    }
}
