using Godot;

namespace Project_Star.Presentation.Playtest;

// 容器边框不参与内容排版，逐帧跟随浮层位置与尺寸。
public sealed partial class LevelBorderView : Panel
{
    internal Control? SurfaceOwner { get; init; }
    public override void _Ready()
    {
        SetProcess(SurfaceOwner is not null);
        FollowBounds();
    }
    public override void _Process(double delta) => FollowBounds();
    internal void FollowBounds()
    {
        if (SurfaceOwner is null) return;
        GlobalPosition = SurfaceOwner.GlobalPosition; Size = SurfaceOwner.Size;
        Scale = SurfaceOwner.GetGlobalTransform().Scale;
        Modulate = SurfaceOwner.Modulate;
        var depth = 8;
        for (Node? node = SurfaceOwner; node is not null; node = node.GetParent())
            if (node is CanvasItem item)
            {
                depth += item.ZIndex;
                if (!item.ZAsRelative || item.TopLevel) break;
            }
        ZAsRelative = false; ZIndex = Mathf.Clamp(depth, -4096, 4096);
    }
}
