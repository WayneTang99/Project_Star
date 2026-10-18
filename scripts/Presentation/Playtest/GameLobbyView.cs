using System;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 场景入口只发出导航意图，保持对局与营地展示状态分离。
public sealed partial class GameLobbyView : Control
{
    public event Action? StartRequested, ResumeRequested, AtlasRequested, SettingsRequested;
    private readonly TextureRect _scene = new() { Name = "Scene", MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
    private readonly Control _warmth = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control _stage = new() { Name = "SceneStage", MouseFilter = MouseFilterEnum.Ignore };
    private Button _start = null!, _resume = null!, _atlas = null!, _settings = null!;
    private Label _title = null!, _brand = null!, _caption = null!;
    private double _seconds;


    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var backdrop = new ColorRect { Color = new Color("101b22"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdropArt = new TextureRect { Texture = GD.Load<Texture2D>("res://art/ui/lobby/traveler-camp-20261018.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/LobbyBackdrop.gdshader") } };
        AddChild(backdropArt); backdropArt.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_stage); _stage.AddChild(_scene); _stage.AddChild(_warmth);
        _warmth.Draw += DrawWarmth;
        _scene.Texture = GD.Load<Texture2D>("res://art/ui/lobby/traveler-camp-20261018.png");
        _atlas = Entry(_stage, "Atlas", "图鉴", () => AtlasRequested?.Invoke());
        _atlas.Icon = GD.Load<Texture2D>("res://art/ui/lobby/book.svg");
        _atlas.AddThemeConstantOverride("icon_max_width", 24); _atlas.AddThemeConstantOverride("h_separation", 12);
        _start = Entry(_stage, "Start", "开始冒险  →", () => StartRequested?.Invoke());
        _start.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("756239ed"), new Color("ebcf8c")));
        _resume = Entry(_stage, "Resume", "继续旅程  →", () => ResumeRequested?.Invoke()); _resume.Hide();
        _title = Text("星辰之旅", 23, MatchTheme.Gold); _brand = Text("PROJECT STAR", 11, new Color("cfcbc0"));
        _caption = Text("旅人营地\n炉火尚暖，远方仍在。", 14, new Color("e9dfc4")); _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _settings = Entry(this, "Settings", "设置", () => SettingsRequested?.Invoke());
        Resized += LayoutScene; LayoutScene(); Hide();
    }
    private Label Text(string value, int size, Color color)
    {
        var label = new Label { Text = value, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(label, size, color, spacing: size == 23 ? 5 : 2); label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .8f));
        label.AddThemeConstantOverride("shadow_offset_x", 1); label.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(label); return label;
    }
    private static Button Entry(Control parent, string name, string text, Action action)
    {
        var button = new Button { Name = name, Text = text }; parent.AddChild(button);
        var normal = MatchTheme.Surface(new Color("172a2ee8"), new Color("d6bd7b99"));
        normal.ContentMarginLeft = normal.ContentMarginRight = 23; normal.ContentMarginTop = normal.ContentMarginBottom = 12;
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", MatchTheme.Surface(new Color("3d4434"), MatchTheme.Gold));
        button.AddThemeFontSizeOverride("font_size", 17); button.AddThemeColorOverride("font_color", new Color("f4e3ba"));
        button.Pressed += action; return button;
    }
    public void Open(bool canResume) { _resume.Visible = canResume; Show(); LayoutScene(); _start.GrabFocus(); }
    private void LayoutScene()
    {
        if (_start is null || Size.X <= 0 || Size.Y <= 0) return;
        var scale = Mathf.Min(Size.X / 1600, Size.Y / 900);
        _stage.Size = new Vector2(1600, 900); _stage.Scale = Vector2.One * scale;
        _stage.Position = (Size - _stage.Size * scale) / 2;
        _scene.Position = Vector2.Zero; _scene.Size = _stage.Size;
        _warmth.Size = _stage.Size;
        _atlas.Size = new Vector2(142, 52); _atlas.Position = new Vector2(368, 648) - _atlas.Size / 2;
        _start.Size = new Vector2(204, 52); _start.Position = new Vector2(1232, 522) - _start.Size / 2;
        _resume.Size = new Vector2(170, 42); _resume.Position = new Vector2(1232 - 85, 558);
        _title.Position = new Vector2(45, 25); _brand.Position = new Vector2(46, 58);
        _settings.Position = new Vector2(Size.X - 118, 28); _settings.Size = new Vector2(82, 42);
        _caption.Position = new Vector2((Size.X - 360) / 2, Size.Y - 76); _caption.Size = new Vector2(360, 54);
        _warmth.QueueRedraw();
    }
    public override void _Process(double delta)
    { if (!IsVisibleInTree()) return; _seconds = (_seconds + delta) % (Math.PI * 8); _warmth.QueueRedraw(); }
    private void DrawWarmth()
    {
        var center = new Vector2(848, 576);
        for (var i = 8; i > 0; i--)
            _warmth.DrawCircle(center, i * 10, new Color(1, .65f, .15f, .002f + .001f * (float)Math.Sin(_seconds * .8)));
        foreach (var anchor in new[] { new Vector2(368, 600), new Vector2(1232, 474) })
            _warmth.DrawLine(anchor - new Vector2(0, 30), anchor, new Color("d6bd7b99"), 1, true);
    }
}

