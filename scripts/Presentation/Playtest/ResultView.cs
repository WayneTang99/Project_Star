using Godot;

namespace Project_Star.Presentation.Playtest;

// 战斗结算与对局终局只展示协调器已完成的结果。
public sealed partial class ResultView : VBoxContainer
{
    private Label _summary = null!;
    private RichTextLabel _log = null!;
    public override void _Ready()
    {
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; AddChild(_summary);
        _log = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false }; AddChild(_log);
    }
    // 刷新结果文字不再次结算战斗。
    public void Render(string message, string log = "")
    {
        _summary.Text = message;
        if (_log.Text != log) _log.Hide();
        _log.Text = log;
    }
    // 日志由开发区域打开，仅改变可见性，不再次执行结算。
    public void ToggleLog() { if (_log.Text.Length > 0) _log.Visible = !_log.Visible; }
}
