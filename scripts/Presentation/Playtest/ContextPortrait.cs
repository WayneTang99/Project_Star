using Godot;

namespace Project_Star.Presentation.Playtest;

// 上排上下文身份区域；尚无正式头像时保留名称占位。
public sealed partial class ContextPortrait : Label
{
    public override void _Ready() => AutowrapMode = TextServer.AutowrapMode.WordSmart;

    // 头像区域与当前页面标题同步。
    public void Render(string title) => Text = title;
}
