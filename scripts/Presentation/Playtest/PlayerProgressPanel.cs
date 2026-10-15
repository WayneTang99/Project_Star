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
    { _feedback.Text = feedback; TooltipText = feedback; _feedback.Hide(); _summary.Show(); }
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
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center; row.TooltipText = caption;
        var key = new StringName(caption == "经验" ? "experience" : caption == "声望" ? "reputation" : "victories");
        row.AddChild(new TextureRect { Texture = MatchTheme.Icon(key), CustomMinimumSize = new Vector2(18, 18), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color("ddbf87"), MouseFilter = MouseFilterEnum.Ignore });
        row.AddThemeConstantOverride("separation", 7);
        _summary.AddChild(row);
        var value = new Label(); MatchTheme.Text(value, 16, new Color("f2dfbd"), true); row.AddChild(value); return value;
    }
    private void Spacer() => _summary.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
    private void LayoutPanel() { _summary.Position = new Vector2(10, 6); _summary.Size = Size - new Vector2(20, 12); _feedback.Size = Size; QueueRedraw(); }
    public override void _Draw() => DrawStyleBox(MatchTheme.Plate("hud-inset", 6), new Rect2(Vector2.Zero, Size));
}
