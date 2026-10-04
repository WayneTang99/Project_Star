using Godot;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 当前阶段标题与等级晶体（表现层）；原画由阶段内容视图展示。
public sealed partial class ContextPortrait : Control
{
    private Label _title = null!;
    private CardLevelGem _level = null!;
    public override void _Ready()
    {
        _title = new Label { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_title); _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _title.AddThemeFontSizeOverride("font_size", 20);
        _level = new CardLevelGem { Name = "EncounterLevelCrystal", Size = new Vector2(30, 40), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_level); _level.Hide();
    }

    // 标题和等级只读取页面快照。
    public void Render(string title, int encounterLevel = 0)
    {
        _title.Text = title;
        _title.TooltipText = encounterLevel > 0 ? $"{title} · 等级 {encounterLevel}" : title;
        _level.Visible = encounterLevel > 0; _level.SetLevel(encounterLevel);
        _level.Position = new Vector2(0, 4);
        _title.OffsetLeft = encounterLevel > 0 ? 40 : 0;
    }
}
