using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Game;

// 左下固定经济区，只读取本轮快照；不重复触发收入结算。
public sealed partial class EconomyView : Control
{
    private Label _wealth = null!;
    private Label _income = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var color = AttributePalette.Find(GameAttributeKeys.Wealth)!.Value;
        var coin = new TextureRect { Texture = MatchTheme.Icon("coin"), Position = new Vector2(15, 12), Size = new Vector2(18, 18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore, Modulate = color, Material = AttributePalette.IconMaterial };
        AddChild(coin);
        _wealth = new Label { Name = "Wealth", Position = new Vector2(36, 4), Size = new Vector2(52, 34), HorizontalAlignment = HorizontalAlignment.Center };
        MatchTheme.Text(_wealth, 24, color, true); AddChild(_wealth);
        _income = new Label { Name = "Income", Position = new Vector2(0, 44), Size = new Vector2(100, 18), HorizontalAlignment = HorizontalAlignment.Center };
        MatchTheme.Text(_income, 11, color); AddChild(_income);
    }
    public void Render(MatchSnapshot? snapshot)
    {
        Visible = snapshot is not null;
        _wealth.Text = snapshot?.Wealth.ToString() ?? "";
        _income.Text = snapshot is null ? "" : $"收入  +{snapshot.Income}";
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawStyleBox(MatchTheme.Plate("dock", 6), new Rect2(Vector2.Zero, Size));
    }
}
