using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 营地内浏览与选择英雄，只在开始冒险时发出创建旅程意图（表现层）。
public sealed partial class GameLobbyView : Control
{
    public event Action? StartRequested, ResumeRequested, AtlasRequested, SettingsRequested;
    public StringName SelectedHeroKey { get; private set; } = new("");
    private readonly Control _stage = new() { Name = "SceneStage", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control _decoration = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly PanelContainer _inspect = new() { Name = "HeroInfo", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 5 };
    private readonly VBoxContainer _info = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _selectedName = new() { Name = "SelectedName", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _selectedTitle = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly TextureRect _portrait = new() { MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
    private readonly List<(KeyedAction Hero, Button Button, Label Name)> _heroes = new();
    private IReadOnlyList<KeyedAction> _entries = Array.Empty<KeyedAction>();
    private Button _start = null!, _resume = null!;
    private Button? _inspected;
    // 活动与摆放仅属于界面，英雄身份和数值来自正式快照。
    private static readonly Dictionary<StringName, (string Art, string Activity, Rect2 Rect)> Camp = new()
    {
        [new("hero.paladin")] = ("paladin", "擦拭战锤", new(.31f, .36f, .16f, .33f)),
        [new("hero.mona")] = ("mona", "调配药剂", new(.15f, .43f, .16f, .32f)),
        [new("hero.robin")] = ("robin", "查看地图", new(.64f, .36f, .14f, .36f)),
        [new("hero.harla")] = ("harla", "修理机械", new(.80f, .44f, .18f, .33f)),
        [new("hero.valos")] = ("valos", "保养匕首", new(.72f, .31f, .13f, .29f)),
        [new("hero.jiyun")] = ("jiyun", "煮茶小憩", new(.42f, .49f, .17f, .30f)),
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var texture = GD.Load<Texture2D>("res://art/ui/lobby/republic-20261019.png");
        var backdrop = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/LobbyBackdrop.gdshader") } };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_stage);
        var scene = new TextureRect { Texture = texture, Size = new Vector2(1600, 900), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        _stage.AddChild(scene); _stage.AddChild(_decoration); _decoration.Draw += DrawDecoration;
        Place(Text(_stage, "✧ 星辰之旅", 32, new("f0dfb8"), 5), 55, 24, 400, 48);
        Place(Text(_stage, "PROJECT STAR", 11, new("c5c9bc"), 5), 107, 73, 340, 22);
        Place(Text(_stage, "THE REPUBLIC", 12, new("bcbfba"), 6), 64, 144, 420, 24);
        Place(Text(_stage, "共和国", 40, new("f2e1bb"), 8), 64, 178, 450, 64);
        Place(Text(_stage, "金色城门下，等一位同行的人。", 14, new("c7c9bd"), 2), 64, 248, 450, 26);
        var atlas = Entry("Atlas", "图鉴", () => AtlasRequested?.Invoke());
        atlas.Icon = GD.Load<Texture2D>("res://art/ui/lobby/book.svg"); atlas.AddThemeConstantOverride("icon_max_width", 16);
        Place(atlas, 1352, 32, 100, 44);
        Place(Entry("Settings", "设置", () => SettingsRequested?.Invoke()), 1465, 32, 79, 44);
        _start = Entry("Start", "开始冒险    →", () => { if (!_start.Disabled) StartRequested?.Invoke(); });
        _start.AddThemeFontSizeOverride("font_size", 20);
        _start.AddThemeColorOverride("font_color", new Color("172823"));
        foreach (var state in new[] { "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            _start.AddThemeColorOverride(state, new Color("172823"));
        _start.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("c4a968"), new Color("f0d698")));
        _start.AddThemeStyleboxOverride("hover", MatchTheme.Surface(new Color("e1c98b"), new Color("ffedb3")));
        Place(_start, 1274, 799, 262, 65);
        _resume = Entry("Resume", "继续旅程 →", () => ResumeRequested?.Invoke());
        _resume.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); Place(_resume, 1100, 808, 158, 44); _resume.Hide();
        Place(Text(_stage, "本次同行", 11, new("d8bf86"), 3), 145, 806, 200, 18);
        _portrait.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/CampPortrait.gdshader") };
        _stage.AddChild(_portrait); Place(_portrait, 64, 804, 65, 65);
        _stage.AddChild(_selectedName); MatchTheme.Text(_selectedName, 25, new Color("eee5cb"), spacing: 3);
        Place(_selectedName, 145, 830, 200, 38);
        _stage.AddChild(_selectedTitle); MatchTheme.Text(_selectedTitle, 14, new Color("bdc8bf")); Place(_selectedTitle, 350, 839, 160, 26);
        Place(Text(_stage, "悬浮了解英雄 · 点击选择同行", 12, new("b9c2b7"), 2), 650, 829, 430, 28);
        _stage.AddChild(_inspect); _inspect.AddChild(_info);
        var panel = MatchTheme.Surface(new Color("162923fa"), new Color("baa06b"));
        panel.ContentMarginLeft = panel.ContentMarginRight = panel.ContentMarginTop = panel.ContentMarginBottom = 24;
        panel.ShadowColor = new Color(0, 0, 0, .5f); panel.ShadowSize = 16; _inspect.AddThemeStyleboxOverride("panel", panel);
        _info.AddThemeConstantOverride("separation", 8); _inspect.Hide();
        VisibilityChanged += () => { if (!IsVisibleInTree()) HideInfo(); };
        Resized += LayoutScene; Render(_entries); LayoutScene(); Hide();
    }

    // 保留同行选择；刷新只消费正式快照，不创建或重置对局。
    public void Render(IReadOnlyList<KeyedAction> entries)
    {
        var changed = !_entries.SequenceEqual(entries); _entries = entries;
        if (!IsNodeReady()) return;
        if (changed || _heroes.Count == 0)
        {
            HideInfo();
            foreach (var (_, button, _) in _heroes) { _stage.RemoveChild(button); button.QueueFree(); }
            _heroes.Clear();
            foreach (var hero in entries.Where(item => item.Action.Visible).OrderBy(item => FactionDisplayOrder.Rank(item.Key)))
            {
                if (!Camp.TryGetValue(hero.Key, out var camp)) continue;
                var button = new Button { Name = new StringName(camp.Art), Disabled = !hero.Action.Enabled,
                    MouseDefaultCursorShape = CursorShape.PointingHand };
                foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
                    button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
                _stage.AddChild(button);
                var art = new TextureRect { Texture = GD.Load<Texture2D>($"res://art/ui/lobby/camp-{camp.Art}-20261019.png"),
                    MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
                button.AddChild(art);
                var plate = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(plate); plate.AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color("0e1e23cc"), new Color("c8b47a66")));
                var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; labels.AddThemeConstantOverride("separation", 2); plate.AddChild(labels);
                var name = Text(labels, hero.Hero?.DisplayName ?? hero.Action.Text, 16, new("eee5cb"), 2); name.HorizontalAlignment = HorizontalAlignment.Center;
                var activity = Text(labels, camp.Activity, 10, new("c6c9b6")); activity.HorizontalAlignment = HorizontalAlignment.Center;
                var rect = new Rect2(camp.Rect.Position * new Vector2(1600, 900), camp.Rect.Size * new Vector2(1600, 900));
                button.Position = rect.Position; button.Size = rect.Size;
                var artSize = art.Texture.GetSize(); artSize *= Mathf.Min(rect.Size.X / artSize.X, rect.Size.Y / artSize.Y);
                art.Size = artSize; art.Position = new Vector2((rect.Size.X - artSize.X) / 2, rect.Size.Y - artSize.Y);
                plate.Position = new Vector2((rect.Size.X - 124) / 2, rect.Size.Y * 1.05f - 54); plate.Size = new Vector2(124, 54);
                button.Pressed += () => { SelectHero(hero.Key); ShowInfo(button); };
                button.MouseEntered += () => { art.Modulate = new Color(1.12f, 1.10f, 1.04f); ShowInfo(button); };
                button.MouseExited += () => { art.Modulate = Colors.White; HideInfo(); };
                button.FocusEntered += () => ShowInfo(button); button.FocusExited += HideInfo;
                button.GuiInput += input => Browse(button, input);
                _heroes.Add((hero, button, name));
            }
            _stage.MoveChild(_inspect, _stage.GetChildCount() - 1);
        }
        if (!_heroes.Any(item => item.Hero.Key == SelectedHeroKey))
            SelectedHeroKey = _heroes.FirstOrDefault().Hero?.Key ?? new StringName("");
        UpdateSelection();
    }
    private void SelectHero(StringName key)
    {
        if (!_heroes.Any(item => item.Hero.Key == key && item.Hero.Action.Enabled)) return;
        SelectedHeroKey = key; UpdateSelection();
    }
    private void UpdateSelection()
    {
        var hero = _heroes.FirstOrDefault(item => item.Hero.Key == SelectedHeroKey).Hero;
        _selectedName.Text = hero?.Hero?.DisplayName ?? "暂无可选英雄"; _selectedTitle.Text = hero?.Hero?.Title ?? "";
        _portrait.Texture = hero is null ? null : PlayerHeroPanel.PortraitTexture(hero.Key, hero.Illustration);
        _start.Disabled = hero is null || !hero.Action.Enabled; _start.TooltipText = hero?.Action.Reason ?? "";
        foreach (var item in _heroes)
        {
            var selected = item.Hero.Key == SelectedHeroKey;
            item.Name.Text = (selected ? "◆ " : "") + (item.Hero.Hero?.DisplayName ?? item.Hero.Action.Text);
            item.Name.AddThemeColorOverride("font_color", new Color(selected ? "ffe4a2" : "eee5cb"));
        }
        _decoration.QueueRedraw();
    }
    private void Browse(Button button, InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) { HideInfo(); _start.GrabFocus(); button.AcceptEvent(); return; }
        var direction = key.Keycode is Key.Left or Key.Up ? -1 : key.Keycode is Key.Right or Key.Down ? 1 : 0;
        if (direction == 0 || _heroes.Count == 0) return;
        var index = _heroes.FindIndex(item => item.Button == button);
        _heroes[(index + direction + _heroes.Count) % _heroes.Count].Button.GrabFocus(); button.AcceptEvent();
    }
    private void ShowInfo(Button button)
    {
        if (!IsVisibleInTree()) return;
        var hero = _heroes.FirstOrDefault(item => item.Button == button).Hero;
        if (hero?.Hero is not { } details) return;
        foreach (var child in _info.GetChildren()) { _info.RemoveChild(child); child.QueueFree(); }
        Text(_info, details.Title, 13, new("d9b777"), 3); Text(_info, details.DisplayName, 32, new("eee5cb"), 3);
        Text(_info, $"正在{Camp[hero.Key].Activity}，等待下一段旅程。", 13, new("c0c6b6"));
        _info.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
        foreach (var (label, value, attribute) in new[] { ("最大生命", details.MaxHealth.ToString(), GameAttributeKeys.MaxHealth),
            ("最大魔法", details.MaxMana.ToString(), GameAttributeKeys.MaxMana), ("魔法再生", $"{details.ManaRegen} / 秒", GameAttributeKeys.ManaRegen),
            ("每轮收入", details.Income.ToString(), GameAttributeKeys.Income) })
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; _info.AddChild(row);
            var color = AttributePalette.Find(attribute) ?? MatchTheme.Ink;
            var caption = Text(row, label, 15, color); caption.SizeFlagsHorizontal = SizeFlags.ExpandFill; Text(row, value, 21, color);
        }
        Text(_info, hero.Key == SelectedHeroKey ? "◆ 已选为同行英雄" : "点击选择同行", 12, new("d9b777"), 2);
        _inspect.CustomMinimumSize = new Vector2(306, 0); _inspect.Size = Vector2.Zero; _inspect.Show(); _inspected = button; PositionInfo();
    }
    private void HideInfo() { _inspect.Hide(); _inspected = null; }
    private void PositionInfo()
    {
        if (_inspected is null) return;
        var rect = _inspected.GetRect(); var width = _inspect.GetCombinedMinimumSize().X;
        var x = rect.End.X - 12; if (x + width > 1585) x = rect.Position.X - width + 12;
        _inspect.Position = new Vector2(Mathf.Max(15, x), Mathf.Clamp(rect.Position.Y, 15, Mathf.Max(15, 885 - _inspect.Size.Y)));
    }
    // 打开大厅保留同行选择；可否继续由外部对局快照决定。
    public void Open(bool canResume) { _resume.Visible = canResume; HideInfo(); Show(); LayoutScene(); _start.GrabFocus(); }
    private void LayoutScene()
    {
        if (_start is null || Size.X <= 0 || Size.Y <= 0) return;
        var scale = Mathf.Min(Size.X / 1600, Size.Y / 900);
        _stage.Size = new Vector2(1600, 900); _stage.Scale = Vector2.One * scale; _stage.Position = (Size - _stage.Size * scale) / 2;
        _decoration.Size = _stage.Size; PositionInfo(); _decoration.QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        PositionInfo(); _decoration.QueueRedraw();
    }
    private void DrawDecoration()
    {
        for (var y = 0; y < 900; y += 4)
        {
            var opacity = y < 220 ? .45f * (1 - y / 220f) : y > 585 ? .75f * (y - 585) / 315f : 0;
            if (opacity > 0) _decoration.DrawRect(new Rect2(0, y, 1600, 4), new Color(.025f, .07f, .08f, opacity));
        }
        _decoration.DrawLine(new Vector2(64, 784), new Vector2(1536, 784), new Color("c7b37c66"), 1);
        var selected = _heroes.FirstOrDefault(item => item.Hero.Key == SelectedHeroKey).Button;
        if (selected is not null)
        {
            var rect = selected.GetRect(); var center = rect.Position + new Vector2(rect.Size.X / 2, rect.Size.Y * .91f);
            var points = Enumerable.Range(0, 65).Select(i => center + new Vector2(Mathf.Cos(i * Mathf.Tau / 64) * rect.Size.X * .36f,
                Mathf.Sin(i * Mathf.Tau / 64) * rect.Size.Y * .055f)).ToArray();
            _decoration.DrawPolyline(points, new Color("e8c97e"), 2, true);
        }
    }
    private Button Entry(string name, string text, Action action)
    {
        var button = new Button { Name = name, Text = text }; _stage.AddChild(button);
        button.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("102128dd"), new Color("9eacaa66")));
        button.AddThemeStyleboxOverride("hover", MatchTheme.Surface(new Color("33434b"), new Color("d5bc80")));
        var focus = MatchTheme.Surface(Colors.Transparent, new Color("fff0bb")); focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeFontSizeOverride("font_size", 15); button.AddThemeColorOverride("font_color", new Color("d9ded8"));
        button.Pressed += action; return button;
    }
    private static Label Text(Control parent, string value, int size, Color color, int spacing = 0)
    {
        var label = new Label { Text = value, MouseFilter = MouseFilterEnum.Ignore }; MatchTheme.Text(label, size, color, spacing: spacing);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .8f));
        label.AddThemeConstantOverride("shadow_offset_y", 2); parent.AddChild(label); return label;
    }
    private static void Place(Control control, float x, float y, float width, float height)
    { control.Position = new Vector2(x, y); control.Size = new Vector2(width, height); }
}
