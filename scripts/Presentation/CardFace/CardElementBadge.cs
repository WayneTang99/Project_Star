using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.CardFace;

// HTML 元素徽章使用圆形渐变与对应符号，元素身份保持 StringName。
public sealed partial class CardElementBadge : Control
{
    private StringName _elementKey = GameElements.General;
    private static readonly Texture2D WindRune = GD.Load<Texture2D>("res://art/ui/card-face/wind-rune.svg");
    public override void _Ready() => Resized += QueueRedraw;
    public override void _ExitTree() => Resized -= QueueRedraw;
    // 根据快照元素刷新徽章。
    public void SetElement(StringName elementKey) { _elementKey = elementKey; QueueRedraw(); }
    public override void _Draw()
    {
        var center = Size / 2; var radius = Mathf.Min(Size.X, Size.Y) / 2;
        DrawCircle(center + new Vector2(0, 2), radius + 2, new Color("00000066"));
        DrawCircle(center, radius + 2, new Color("47381f"));
        DrawCircle(center, radius, new Color("ffe2a0"));
        var (bright, dark, glyph) = _elementKey == GameElements.Light ? ("ffffff", "dfe3e7", "☼")
            : _elementKey == GameElements.Wood ? ("9ed76a", "327d3d", "♧")
            : _elementKey == GameElements.Water ? ("548ee4", "153f9c", "≈")
            : _elementKey == GameElements.Fire ? ("ff855b", "b33225", "♨")
            : _elementKey == GameElements.Wind ? ("e9fff0", "85baa4", "")
            : _elementKey == GameElements.Earth ? ("c49963", "70502f", "△")
            : _elementKey == GameElements.Lightning ? ("fff976", "c0a822", "ϟ")
            : _elementKey == GameElements.Ice ? ("b4ffff", "2cafb8", "❄")
            : _elementKey == GameElements.Dark ? ("a07ccc", "492d70", "☽")
            : ("a8adb3", "62584e", "◇");
        DrawCircle(center, radius - 1, new Color(dark));
        for (var i = 24; i > 0; i--)
            DrawCircle(center + new Vector2(-radius * .25f, -radius * .35f) * (1 - i / 24f),
                (radius - 1) * i / 24f, new Color(dark).Lerp(new Color(bright), 1 - i / 24f));
        if (_elementKey == GameElements.Wind)
        {
            var runeSize = Vector2.One * (radius * 2 * 19 / 23);
            DrawTextureRect(WindRune, new Rect2(center - runeSize / 2, runeSize), false);
            return;
        }
        var runeFont = Mathf.RoundToInt(15 * Size.X / 23);
        var font = MatchTheme.Font(true); var textWidth = font.GetStringSize(glyph, fontSize: runeFont).X;
        var runeColor = _elementKey == GameElements.Light ? "b38427" : _elementKey == GameElements.Lightning ? "594500" : "fff9e0";
        DrawString(font, new Vector2((Size.X - textWidth) / 2, center.Y + 5 * Size.Y / 23), glyph, fontSize: runeFont, modulate: new Color(runeColor));
    }
}
