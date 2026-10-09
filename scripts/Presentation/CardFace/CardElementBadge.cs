using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.CardFace;

// HTML 元素徽章使用圆形渐变与对应符号，元素身份保持 StringName。
public sealed partial class CardElementBadge : Control
{
    private StringName _elementKey = GameElements.General;
    // 根据快照元素刷新徽章。
    public void SetElement(StringName elementKey) { _elementKey = elementKey; QueueRedraw(); }
    public override void _Draw()
    {
        var center = Size / 2; var radius = Mathf.Min(Size.X, Size.Y) / 2;
        DrawCircle(center + new Vector2(0, 2), radius + 2, new Color("00000066"));
        DrawCircle(center, radius + 2, new Color("47381f"));
        DrawCircle(center, radius, new Color("ffe2a0"));
        var (bright, dark, glyph) = _elementKey == GameElements.Light ? ("ffedaf", "bb8037", "☼")
            : _elementKey == GameElements.Wood ? ("b5d674", "3c7952", "♧")
            : _elementKey == GameElements.Water ? ("a5e3ef", "346b9b", "≈")
            : _elementKey == GameElements.Fire ? ("ffc595", "9e4a32", "♨")
            : _elementKey == GameElements.Wind ? ("d2f0c8", "578a76", "≋")
            : _elementKey == GameElements.Earth ? ("e5d0a0", "937345", "△")
            : _elementKey == GameElements.Lightning ? ("fff7b2", "b29a40", "ϟ")
            : _elementKey == GameElements.Ice ? ("d7f8f9", "599fac", "❄")
            : _elementKey == GameElements.Dark ? ("d7bfe8", "6f4983", "☽")
            : ("dbe0d4", "657675", "◇");
        DrawCircle(center, radius - 1, new Color(dark));
        for (var i = 24; i > 0; i--)
            DrawCircle(center + new Vector2(-radius * .25f, -radius * .35f) * (1 - i / 24f),
                (radius - 1) * i / 24f, new Color(dark).Lerp(new Color(bright), 1 - i / 24f));
        var font = MatchTheme.Font(true); var textWidth = font.GetStringSize(glyph, fontSize: 15).X;
        DrawString(font, new Vector2((Size.X - textWidth) / 2, center.Y + 5), glyph, fontSize: 15, modulate: new Color("fff9e0"));
    }
}
