using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 英雄侧栏底部成长信息展示，不自行推导升级规则。
public sealed partial class PlayerProgressPanel : VBoxContainer
{
    private readonly Label _summary = new();
    private ResourceBar _experience = null!;
    private readonly Label _feedback = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public override void _Ready()
    {
        _experience = new ResourceBar("ExperienceBar", new Color("e2c379")); AddChild(_experience);
        AddChild(_summary);
        _feedback.MaxLinesVisible = 2;
        AddChild(_feedback);
    }
    // 回放反馈只显示最近两行，悬停可读完整文本。
    public void RenderPlayback(string feedback)
    {
        _feedback.Text = feedback;
        _feedback.TooltipText = feedback;
    }
    // 成长与经济组件接受同一份捕获结果。
    public void Render(MatchSnapshot? snapshot)
    {
        _summary.Text = snapshot is null ? "" : $"声望 {snapshot.Reputation} · 胜场 {snapshot.PvpWins}/10";
        _experience.Visible = snapshot?.Hero is not null;
        if (snapshot is not null) _experience.Render("经验", snapshot.Experience, 10);
        _feedback.Text = "";
        _feedback.TooltipText = "";
    }
}
