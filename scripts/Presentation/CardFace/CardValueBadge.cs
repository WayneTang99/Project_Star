using Godot;

namespace Project_Star.Presentation.CardFace;

// 持有价值徽标，按卡面实际大小设置文字和图标。
public sealed partial class CardValueBadge : HBoxContainer
{
    private Label _value = null!;

    public override void _Ready()
    {
        _value = GetNode<Label>("Value");
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
        GetNode<TextureRect>("Icon").CustomMinimumSize = Vector2.One * Mathf.Max(8, font * .65f);
        AddThemeConstantOverride("separation", 1);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.HorizontalAlignment = HorizontalAlignment.Left;
        _value.ClipText = true;
    }
}
