using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 顶栏展示当前轮、八回合轨迹与经济快照；轨迹是状态提示。
public sealed partial class TopBar : HBoxContainer
{
    private Label _turn = null!;
    private HBoxContainer _route = null!;
    private Label _wealth = null!;
    private Label _income = null!;
    private HBoxContainer _economy = null!;

    public override void _Ready()
    {
        _turn = new Label { Name = "State", CustomMinimumSize = new Vector2(156, 0),
            VerticalAlignment = VerticalAlignment.Center };
        _turn.AddThemeFontSizeOverride("font_size", 16); AddChild(_turn);
        _route = new HBoxContainer { Name = "RoundPath", Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill }; AddChild(_route);
        _route.AddThemeConstantOverride("separation", 12);
        for (var index = 1; index <= 8; index++)
        {
            var marker = new PanelContainer { Name = $"Turn{index}", CustomMinimumSize = new Vector2(34, 32),
                TooltipText = index == 4 ? "第4回合 · 怪物战" : index == 8 ? "第8回合 · 玩家对战" : $"第{index}回合 · 选择遭遇" };
            _route.AddChild(marker);
            var number = new Label { Text = index.ToString(), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            number.AddThemeFontSizeOverride("font_size", 13);
            if (index is 4 or 8)
            {
                var content = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
                content.AddThemeConstantOverride("separation", 2); marker.AddChild(content); content.AddChild(number);
                content.AddChild(new TextureRect { Texture = MatchTheme.Icon(index == 4 ? new StringName("battle") : new StringName("wins")),
                    CustomMinimumSize = new Vector2(12, 12), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
            }
            else marker.AddChild(number);
        }
        _economy = new HBoxContainer { Name = "Economy", Alignment = BoxContainer.AlignmentMode.End };
        AddChild(_economy);
        var wealth = MatchTheme.Stat("coin", "", "持有金币");
        var income = MatchTheme.Stat("continue", "", "每回合收入");
        _economy.AddChild(wealth); _economy.AddChild(income);
        _wealth = wealth.GetChild<Label>(1); _income = income.GetChild<Label>(1);
    }

    // 访问中的遭遇使用捕获的轮回合，经济仍来自最新快照。
    public void Render(MatchSnapshot? snapshot, int displayRound = 0, int displayTurn = 0)
    {
        var turn = displayTurn > 0 ? displayTurn : snapshot?.Turn ?? 0;
        _turn.Text = snapshot is null ? "Project Star" : $"第 {(displayRound > 0 ? displayRound : snapshot.Round)} 轮";
        _route.Visible = snapshot is not null;
        _economy.Visible = snapshot is not null;
        _wealth.Text = snapshot?.Wealth.ToString() ?? "";
        _income.Text = snapshot is null ? "" : $"+{snapshot.Income}";
        for (var index = 0; index < _route.GetChildCount(); index++)
        {
            var current = turn == index + 1;
            var marker = _route.GetChild<PanelContainer>(index);
            marker.AddThemeStyleboxOverride("panel", MatchTheme.Surface(current ? new Color("d6edf7") : new Color("f7f8f3"),
                current ? MatchTheme.Blue : index + 1 is 4 or 8 ? MatchTheme.Gold : new Color("cbd6d9")));
            marker.Modulate = snapshot is not null && index + 1 < turn ? new Color(1, 1, 1, .55f) : Colors.White;
        }
    }
}
