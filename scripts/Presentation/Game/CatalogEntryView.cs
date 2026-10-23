using Godot;

namespace Project_Star.Presentation.Game;

// 图鉴卡片的独立底板，保持内容布局和卡面尺寸分离（表现层）。
public sealed partial class CatalogEntryView : VBoxContainer
{
    private StyleBox _plate = HtmlReliefStyle.Surface();
    public override void _Ready() { AddThemeConstantOverride("separation", 8); Resized += QueueRedraw; }
    // 卡面反馈只切换绘制样式，不改变单元格与卡面的几何尺寸。
    public void ShowState(string state) { _plate = HtmlReliefStyle.Surface(state); QueueRedraw(); }
    public override void _Draw() => DrawStyleBox(_plate, new Rect2(Vector2.Zero, Size));
    public override void _ExitTree() => Resized -= QueueRedraw;
}
