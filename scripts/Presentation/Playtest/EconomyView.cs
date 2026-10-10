using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 左下固定经济区，只读取本轮快照；不重复触发收入结算。
public sealed partial class EconomyView : Control
{
    private Label _wealth = null!;
    private Label _income = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var color = AttributePalette.Find(GameAttributeKeys.Wealth)!.Value;
        var title = new Label { Text = "旅途资金", Position = new Vector2(16, 10) };
        MatchTheme.Text(title, 11, MatchTheme.Muted); AddChild(title);
        _income = new Label { Name = "Income", Position = new Vector2(16, 34) };
        MatchTheme.Text(_income, 15, color, bold: true); AddChild(_income);
        var coin = new TextureRect { Texture = MatchTheme.Icon("coin"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Size = new Vector2(19, 19), MouseFilter = MouseFilterEnum.Ignore, Modulate = color, Material = AttributePalette.IconMaterial };
        AddChild(coin);
        _wealth = new Label { Name = "Wealth", Size = new Vector2(110, 38), HorizontalAlignment = HorizontalAlignment.Right };
        MatchTheme.Text(_wealth, 24, color, true); AddChild(_wealth);
        Resized += Layout;
        Layout();
        void Layout() { _wealth.Position = new Vector2(Size.X - 126, 15); coin.Position = new Vector2(Size.X - 155, 25); }
    }
    public void Render(MatchSnapshot? snapshot)
    {
        Visible = snapshot is not null;
        _wealth.Text = snapshot?.Wealth.ToString() ?? "";
        _income.Text = snapshot is null ? "" : $"每轮收入  +{snapshot.Income}";
        QueueRedraw();
    }
    public override void _Draw() => MatchTheme.DrawSurface(this, new Rect2(Vector2.Zero, Size), "hero");
}
