using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 英雄成长摘要沿用 HTML 的三列文字层级，数据来自同一次快照。
public sealed partial class PlayerProgressPanel : Control
{
    private HBoxContainer _summary = null!;
    private Label _experience = null!;
    private Label _reputation = null!;
    private Label _wins = null!;
    private Label _feedback = null!;
    public override void _Ready()
    {
        _summary = new HBoxContainer(); AddChild(_summary);
        _experience = Entry("经验"); Spacer(); _reputation = Entry("声望"); Spacer(); _wins = Entry("胜场");
        _feedback = new Label { Visible = false, ClipText = true }; MatchTheme.Text(_feedback, 10, MatchTheme.Muted); AddChild(_feedback);
        Resized += LayoutPanel; LayoutPanel();
    }
    // 回放文本保留完整悬停内容，常态显示三项成长值。
    public void RenderPlayback(string feedback)
    { _feedback.Text = feedback; _feedback.TooltipText = feedback; _feedback.Visible = feedback.Length > 0; _summary.Visible = feedback.Length == 0; }
    // 成长数值来自只读对局快照。
    public void Render(MatchSnapshot? snapshot)
    {
        _experience.Text = snapshot is null ? "" : $"{snapshot.Experience} / 10";
        _reputation.Text = snapshot?.Reputation.ToString() ?? "";
        _wins.Text = snapshot is null ? "" : $"{snapshot.PvpWins} / 10";
        _feedback.Hide(); _summary.Show(); LayoutPanel();
    }
    public override void _ExitTree() => Resized -= LayoutPanel;
    private Label Entry(string caption)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4); _summary.AddChild(row);
        var label = new Label { Text = caption }; MatchTheme.Text(label, 10, new Color("9dae9b")); row.AddChild(label);
        var value = new Label(); MatchTheme.Text(value, 14, new Color("d4ba80"), true); row.AddChild(value); return value;
    }
    private void Spacer() => _summary.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
    private void LayoutPanel() { _summary.Size = Size; _feedback.Size = Size; }
}
