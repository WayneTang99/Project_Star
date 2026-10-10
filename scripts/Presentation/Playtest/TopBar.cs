using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 顶栏还原 HTML 品牌、菱形回合轨迹与经济层级，数值读取当前快照。
public sealed partial class TopBar : HBoxContainer
{
    private Control _journey = null!;
    private Label _round = null!;
    private Label _turn = null!;
    private int _currentTurn;
    public override void _Ready()
    {
        var brand = new Control { Name = "Brand", CustomMinimumSize = new Vector2(220, 82), MouseFilter = MouseFilterEnum.Ignore }; AddChild(brand);
        var mark = new Label { Text = "✧", Position = new Vector2(0, 17), Size = new Vector2(30, 48) }; MatchTheme.Text(mark, 34, MatchTheme.Gold, true); brand.AddChild(mark);
        var title = new Label { Text = "PROJECT STAR", Position = new Vector2(41, 19), Size = new Vector2(190, 26) }; MatchTheme.Text(title, 18, MatchTheme.Ink, true, spacing: 4); brand.AddChild(title);
        var subtitle = new Label { Text = "星 辰 之 旅", Position = new Vector2(41, 46), Size = new Vector2(190, 14) }; MatchTheme.Text(subtitle, 9, MatchTheme.Muted, spacing: 4); brand.AddChild(subtitle);
        AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _journey = new Control { Name = "RoundPath", CustomMinimumSize = new Vector2(265, 82), MouseFilter = MouseFilterEnum.Ignore }; AddChild(_journey);
        _round = new Label { Name = "State", Position = new Vector2(0, 17), Size = new Vector2(75, 18) }; MatchTheme.Text(_round, 12, MatchTheme.Muted); _journey.AddChild(_round);
        _turn = new Label { Position = new Vector2(0, 38), Size = new Vector2(76, 26) }; MatchTheme.Text(_turn, 21, MatchTheme.Gold, true); _journey.AddChild(_turn);
        for (var index = 0; index < 8; index++)
            _journey.AddChild(new Control { Name = $"Turn{index + 1}", Position = new Vector2(92 + 23 * index, 36), Size = new Vector2(12, 12),
                TooltipText = index == 3 ? "第4回合 · 怪物战" : index == 7 ? "第8回合 · 玩家对战" : $"第{index + 1}回合" });
        AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new Control { Name = "MenuSpace", CustomMinimumSize = new Vector2(96, 82), MouseFilter = MouseFilterEnum.Ignore });
        Resized += Redraw;
        _journey.ItemRectChanged += Redraw;
    }

    // 展示访问时的轮回合，经济来自最新冻结快照。
    public void Render(MatchSnapshot? snapshot, int displayRound = 0, int displayTurn = 0)
    {
        _currentTurn = displayTurn > 0 ? displayTurn : snapshot?.Turn ?? 0;
        _round.Text = $"第 {(displayRound > 0 ? displayRound : snapshot?.Round ?? 0)} 轮";
        _turn.Text = $"{_currentTurn:00} / 08";
        _journey.Visible = snapshot is not null; QueueRedraw();
    }

    public override void _Draw()
    {
        if (_journey is null || !_journey.Visible) return;
        for (var index = 0; index < 8; index++)
        {
            var center = _journey.Position + new Vector2(98 + 23 * index, 41);
            Vector2[] diamond = [center + new Vector2(0, -6), center + new Vector2(6, 0), center + new Vector2(0, 6), center + new Vector2(-6, 0)];
            var current = _currentTurn == index + 1;
            DrawColoredPolygon(diamond, current ? new Color("ffedb3") : index + 1 < _currentTurn ? new Color("ba9c62") : new Color("1b2a25"));
            DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], new Color("9f9460"), 1, true);
            if (current) DrawCircle(center, 8, new Color(1, .85f, .4f, .12f));
            if (index < 7) DrawLine(center + new Vector2(10, 11), center + new Vector2(17, 11), new Color("87794b"), 1);
        }
    }
    public override void _ExitTree() { Resized -= Redraw; _journey.ItemRectChanged -= Redraw; }
    private void Redraw() => QueueRedraw();
}
