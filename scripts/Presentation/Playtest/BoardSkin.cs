using Godot;

namespace Project_Star.Presentation.Playtest;

// 棋盘皮肤仅提供纹理和装饰材质；布局、槽位与命中区域由MatchShell独立持有（表现层）。
[GlobalClass]
public partial class BoardSkin : Resource
{
    [Export] public Texture2D TableFrame { get; set; } = null!;
    [Export] public Texture2D BoardTrack { get; set; } = null!;
    [Export] public Texture2D TableSurface { get; set; } = null!;
    [Export] public Texture2D PortraitMount { get; set; } = null!;
    [Export] public ShaderMaterial SurfaceEffect { get; set; } = null!;
}
