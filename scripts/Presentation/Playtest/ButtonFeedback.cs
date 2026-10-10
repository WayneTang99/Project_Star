using Godot;

namespace Project_Star.Presentation.Playtest;

// 按钮表现层反馈绘制在插画上方；鼠标、键盘和禁用共用原按钮状态。
public sealed partial class ButtonFeedback : Control
{
    private BaseButton _button = null!;
    private float _hover;
    private float _pressed;

    public override void _Ready()
    {
        _button = GetParent<BaseButton>();
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 10;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        var enabled = !_button.Disabled && _button.IsVisibleInTree();
        var hovered = enabled && (_button.IsHovered() || _button.HasFocus());
        var pressed = enabled && _button.GetDrawMode() is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed;
        var cursor = enabled ? CursorShape.PointingHand : CursorShape.Arrow;
        if (_button.MouseDefaultCursorShape != cursor) _button.MouseDefaultCursorShape = cursor;
        var hover = Mathf.MoveToward(_hover, hovered ? 1 : 0, (float)delta / .12f);
        var down = pressed ? 1f : 0f;
        if (Mathf.IsEqualApprox(_hover, hover) && _pressed == down) return;
        _hover = hover; _pressed = down; QueueRedraw();
    }

    public override void _Draw()
    {
        if (_hover <= 0 && _pressed <= 0) return;
        var rect = new Rect2(Vector2.Zero, Size);
        if (_button is CardItemView)
        {
            var face = _button.GetChild<Control>(0);
            rect = face.GetRect();
            if (_pressed > 0) DrawRect(rect, new Color(0, 0, 0, .22f));
            return;
        }
        var color = _button.HasFocus() ? MatchTheme.Blue : MatchTheme.Gold;
        var style = MatchTheme.Surface(_pressed > 0 ? new Color(0, 0, 0, .22f) : new Color(1, 1, 1, .08f * _hover),
            new Color(_pressed > 0 ? MatchTheme.Blue : color, Mathf.Max(_hover, _pressed)));
        style.ShadowColor = new Color(color, .2f * _hover); style.ShadowSize = 6;
        DrawStyleBox(style, rect.Grow(-1));
    }
}
