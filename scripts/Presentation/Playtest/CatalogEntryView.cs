using Godot;

namespace Project_Star.Presentation.Playtest;

// 图鉴卡片的独立底板，保持内容布局和卡面尺寸分离（表现层）。
public sealed partial class CatalogEntryView : VBoxContainer
{
    private readonly StyleBoxFlat _plate = MatchTheme.Surface(new Color("252b34"), new Color("a88e6544"));
    public override void _Ready() { _plate.SetCornerRadiusAll(8); AddThemeConstantOverride("separation", 8); Resized += QueueRedraw; }
    public override void _Draw() => DrawStyleBox(_plate, new Rect2(Vector2.Zero, Size));
    public override void _ExitTree() => Resized -= QueueRedraw;
}
