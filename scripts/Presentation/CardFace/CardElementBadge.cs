using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

public sealed partial class CardElementBadge : Control
{
    private StringName _elementKey = GameElements.General;

    public void SetElement(StringName elementKey)
    {
        _elementKey = elementKey;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.46f;
        var color = ElementColor(_elementKey);
        DrawCircle(center + new Vector2(0, 2), radius, new Color("#100d0a"));
        DrawCircle(center, radius, new Color("#271b11"));
        DrawCircle(center, radius - 2, new Color("#edcf88"));
        DrawCircle(center, radius - 5, new Color("#75502a"));
        DrawCircle(center, radius - 7, new Color("#17181a"));
        DrawCircle(center, radius - 10, color.Darkened(0.23f));
        DrawCircle(center + new Vector2(0, 1), radius - 12, color);
        DrawCircle(center + new Vector2(-radius * 0.2f, -radius * 0.27f), radius * 0.48f, new Color(1, 1, 1, 0.15f));
        DrawCircle(center + new Vector2(radius * 0.17f, radius * 0.23f), radius * 0.52f, new Color(0.05f, 0.07f, 0.10f, 0.13f));
        DrawArc(center, radius - 5, -2.8f, -0.42f, 32, new Color("#fff0c6"), 1.5f, true);
        DrawArc(center, radius - 5, 0.35f, 2.65f, 32, new Color("#50351c"), 1.4f, true);
        DrawArc(center, radius - 12, -2.75f, -0.6f, 32, new Color(1, 1, 1, 0.72f), 1.1f, true);

        if (_elementKey == GameElements.Light)
        {
            for (var index = 0; index < 10; index++)
            {
                var angle = Mathf.Tau * index / 10f;
                var inner = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.35f;
                var outer = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.68f;
                DrawLine(inner, outer, new Color("#fff0a8"), 1.35f, true);
            }
            DrawCircle(center, radius * 0.24f, new Color("#fff0b0"));
            DrawCircle(center, radius * 0.12f, new Color("#d89b35"));
        }
    }

    private static Color ElementColor(StringName key)
    {
        if (key == GameElements.General) return new Color("#c8cbd0");
        if (key == GameElements.Fire) return new Color("#d75b38");
        if (key == GameElements.Water) return new Color("#4e91cb");
        if (key == GameElements.Wind) return new Color("#77b9ae");
        if (key == GameElements.Earth) return new Color("#a67b45");
        if (key == GameElements.Lightning) return new Color("#e2bf4f");
        if (key == GameElements.Wood) return new Color("#77904a");
        if (key == GameElements.Ice) return new Color("#8dcbd3");
        if (key == GameElements.Light) return new Color("#d8a83d");
        if (key == GameElements.Dark) return new Color("#73518f");
        return new Color("#c8cbd0");
    }
}
