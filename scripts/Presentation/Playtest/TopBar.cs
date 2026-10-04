using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 顶栏从同次快照展示轮回合与经济字段。
public sealed partial class TopBar : HBoxContainer
{
    private Label _turn = null!;
    private Label _economy = null!;
    public override void _Ready()
    {
        _turn = new Label { Name = "State", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _economy = new Label { Name = "Economy", HorizontalAlignment = HorizontalAlignment.Right };
        AddChild(_turn); AddChild(_economy);
    }

    // 刷新文本不读取可变对局。
    public void Render(MatchSnapshot? snapshot)
    {
        _turn.Text = snapshot is null ? "Project Star　选择英雄" : $"第 {snapshot.Round} 轮　·　回合 {snapshot.Turn}/8";
        _economy.Text = snapshot is null ? "" : $"金币 {snapshot.Wealth}　·　收入 +{snapshot.Income}";
    }
}
