using Godot;

namespace Project_Star.Presentation.CardFace;

public sealed partial class CardLevelGem : Control
{
    private int _level = 1;

    public void SetLevel(int level)
    {
        _level = Mathf.Clamp(level, 1, 5);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var halfWidth = Size.X * 0.46f;
        var halfHeight = Size.Y * 0.48f;
        var points = new[]
        {
            center + new Vector2(0, -halfHeight),
            center + new Vector2(halfWidth, 0),
            center + new Vector2(0, halfHeight),
            center + new Vector2(-halfWidth, 0),
        };
        var color = LevelColor(_level);
        DrawColoredPolygon(points, new Color("#513716"));
        var mount = new[]
        {
            center + new Vector2(0, -halfHeight + 2),
            center + new Vector2(halfWidth - 2, 0),
            center + new Vector2(0, halfHeight - 2),
            center + new Vector2(-halfWidth + 2, 0),
        };
        DrawColoredPolygon(mount, new Color("#d0a456"));
        var inner = new[]
        {
            center + new Vector2(0, -halfHeight + 5),
            center + new Vector2(halfWidth - 5, 0),
            center + new Vector2(0, halfHeight - 5),
            center + new Vector2(-halfWidth + 5, 0),
        };
        DrawColoredPolygon(inner, color);
        DrawColoredPolygon(
            [center + new Vector2(-1, -halfHeight + 6), center + new Vector2(0, 1), center + new Vector2(-halfWidth + 6, 0)],
            color.Lightened(0.48f));
        DrawColoredPolygon(
            [center + new Vector2(1, -halfHeight + 6), center + new Vector2(halfWidth - 6, 0), center + new Vector2(0, halfHeight - 6)],
            color.Darkened(0.26f));
        DrawColoredPolygon(
            [center + new Vector2(-1, -halfHeight + 6), center + new Vector2(0, -halfHeight + 14), center + new Vector2(1, -halfHeight + 6)],
            new Color(1, 1, 1, 0.82f));
        for (var index = 0; index < points.Length; index++)
            DrawLine(points[index], points[(index + 1) % points.Length], new Color("#fff0bf"), 1.3f, true);
    }

    private static Color LevelColor(int level) => level switch
    {
        1 => new Color("#e6e7e5"),
        2 => new Color("#348bd2"),
        3 => new Color("#9254c8"),
        4 => new Color("#e28a30"),
        5 => new Color("#c8423d"),
        _ => new Color("#e6e7e5"),
    };
}
