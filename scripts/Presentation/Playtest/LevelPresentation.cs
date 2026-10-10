using Godot;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 等级边框独立于主题、悬停和焦点；无等级实体不显示此层。
internal static class LevelPresentation
{
    internal static void Frame(Control owner, int level, int radius = 0)
    {
        var frame = owner.GetNodeOrNull<Panel>("LevelBorder");
        if (frame is null)
        {
            frame = new LevelBorderView { Name = "LevelBorder", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 8,
                TopLevel = owner is Container, SurfaceOwner = owner is Container ? owner : null };
            owner.AddChild(frame);
            frame.SetAnchorsAndOffsetsPreset(owner is Container ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.FullRect);
        }
        frame.Visible = level > 0;
        var style = MatchTheme.Outline(CardLevelGem.LevelColor(level));
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(radius);
        frame.AddThemeStyleboxOverride("panel", style);
        if (frame is LevelBorderView view) view.FollowBounds();
    }
}
