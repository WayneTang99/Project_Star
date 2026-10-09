using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 底部英雄栏的成长摘要与回放反馈，不自行推导升级规则。
public sealed partial class PlayerProgressPanel : Control
{
    private readonly HBoxContainer _summary = new();
    private Label _reputation = null!;
    private Label _wins = null!;
    private ResourceBar _experience = null!;
    private readonly Label _feedback = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public override void _Ready()
    {
        _experience = new ResourceBar("ExperienceBar", new Color("e2c379")); AddChild(_experience);
        _experience.SetDisplayHeight(20);
        AddChild(_summary);
        var reputation = MatchTheme.Stat("reputation", "", "声望");
        var wins = MatchTheme.Stat("wins", "", "玩家对战胜场 / 10");
        _summary.AddChild(reputation); _summary.AddChild(wins);
        _reputation = reputation.GetChild<Label>(1); _wins = wins.GetChild<Label>(1);
        _feedback.AddThemeFontSizeOverride("font_size", 16);
        _feedback.MaxLinesVisible = 1;
        AddChild(_feedback);
        Resized += LayoutPanel; LayoutPanel();
    }
    // 回放反馈占用成长摘要所在的一行，悬停可读完整文本。
    public void RenderPlayback(string feedback)
    {
        _feedback.Text = feedback;
        _feedback.TooltipText = feedback;
        _feedback.Visible = feedback.Length > 0;
        _summary.Visible = feedback.Length == 0;
        LayoutPanel();
    }
    // 成长与经济组件接受同一份捕获结果。
    public void Render(MatchSnapshot? snapshot)
    {
        _reputation.Text = snapshot?.Reputation.ToString() ?? "";
        _wins.Text = snapshot is null ? "" : $"{snapshot.PvpWins}/10";
        _experience.Visible = snapshot?.Hero is not null;
        if (snapshot is not null) _experience.Render("经验", snapshot.Experience, 10);
        _feedback.Text = "";
        _feedback.TooltipText = "";
        _feedback.Hide();
        _summary.Show();
        LayoutPanel();
    }

    public override void _ExitTree() => Resized -= LayoutPanel;

    private void LayoutPanel()
    {
        if (_experience is null) return;
        _experience.Position = Vector2.Zero; _experience.Size = new Vector2(Mathf.Min(150, Size.X * .3f), 20);
        _summary.Position = new Vector2(_experience.Size.X + 20, -3);
        _summary.Size = new Vector2(Mathf.Max(1, Size.X - _summary.Position.X), 26);
        _feedback.Position = _summary.Position; _feedback.Size = new Vector2(_summary.Size.X, 22);
    }
}
