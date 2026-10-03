using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 右下成长信息展示，不自行推导升级规则。
public sealed partial class PlayerProgressPanel : VBoxContainer
{
    private readonly Label _summary = new();
    private ResourceBar _experience = null!;
    private readonly Label _feedback = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public override void _Ready()
    {
        AddChild(_summary);
        _experience = new ResourceBar("ExperienceBar", new Color("e2c379")); AddChild(_experience);
        AddChild(_feedback);
    }
    // 回放期间在右下显示最近发生的伤害、治疗、充能或摧毁反馈。
    public void RenderPlayback(string feedback)
    {
        _feedback.Text = feedback;
    }
    // 成长与经济组件接受同一份捕获结果。
    public void Render(MatchSnapshot? snapshot)
    {
        _summary.Text = snapshot is null ? "" : $"等级 {snapshot.Hero?.Level}\n声望 {snapshot.Reputation} · PvP {snapshot.PvpWins}/10";
        _experience.Visible = snapshot?.Hero is not null;
        if (snapshot is not null) _experience.Render("经验", snapshot.Experience, 10);
        _feedback.Text = "";
    }
}
