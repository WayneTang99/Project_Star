using Godot;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 右下成长信息展示，不自行推导升级规则。
public sealed partial class PlayerProgressPanel : Label
{
    // 回放期间在右下显示最近发生的伤害、治疗、充能或摧毁反馈。
    public void RenderPlayback(string feedback)
    {
        Text = feedback;
        AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }
    // 成长与经济组件接受同一份捕获结果。
    public void Render(MatchSnapshot? snapshot) => Text = snapshot is null ? ""
        : $"声望 {snapshot.Reputation}\n经验 {snapshot.Experience}\n等级 {snapshot.Hero?.Level}\nPvP {snapshot.PvpWins}/10";
}
