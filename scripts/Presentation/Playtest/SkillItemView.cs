using Godot;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 方形技能图标组件，图鉴与已获得技能共用只读快照（表现层）。
public sealed partial class SkillItemView : Button
{
    private readonly TextureRect _artwork = new() { Name = "Artwork", MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };

    public override void _Ready()
    {
        AddChild(_artwork); _artwork.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _artwork.OffsetLeft = _artwork.OffsetTop = 6; _artwork.OffsetRight = _artwork.OffsetBottom = -6;
    }

    // 资源只由表现层加载，缺图时复用占位图。
    public void Render(SkillSnapshot skill, CardDisplayAdapter adapter)
    {
        _artwork.Texture = adapter.Artwork(skill.Illustration);
        TooltipText = CardDisplayAdapter.SkillDetails(skill);
        LevelPresentation.Frame(this, skill.Level, 3);
    }

    // 选中轮廓只影响展示，不执行技能能力。
    public void SetSelected(bool selected) => AddThemeStyleboxOverride("normal",
        MatchTheme.Surface(selected ? new Color(MatchTheme.BluePalette ? "314c57" : "354c38") : MatchTheme.SurfaceColor,
            selected ? MatchTheme.Gold : new Color("a5bcc9")));

    public override GodotObject _MakeCustomTooltip(string forText)
    {
        var text = new RichTextLabel { FitContent = true, ScrollActive = false,
            CustomMinimumSize = new Vector2(420, 0), MouseFilter = MouseFilterEnum.Ignore };
        CardKeywordText.Render(text, forText);
        var panel = new PanelContainer(); panel.AddChild(text);
        var color = GetNodeOrNull<Panel>("LevelBorder")?.GetThemeStylebox("panel") as StyleBoxFlat;
        var style = MatchTheme.Surface(MatchTheme.SurfaceColor, color?.BorderColor ?? MatchTheme.Gold);
        style.SetBorderWidthAll(2); panel.AddThemeStyleboxOverride("panel", style); return panel;
    }
}
