using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Game;

// 营地内浏览与选择英雄，只在开始冒险时发出创建旅程意图（表现层）。
public sealed partial class GameLobbyView : Control
{
    public event Action? StartRequested, ResumeRequested, AtlasRequested, SettingsRequested, OnlineRequested;
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
    private Button _heroButton = null!;
    private bool _hasChosenHero;
    private readonly List<Control> _heroDetails = new();
    private readonly Control _chooser = new() { Name = "HeroChooser", Visible = false, ZIndex = 20 };
    private readonly PanelContainer _choicePanel = new() { Name = "Panel" };
    private readonly HBoxContainer _choices = new() { Name = "Choices" };
    private readonly List<(KeyedAction Hero, Button Button)> _choiceButtons = new();
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
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/LobbyBackdrop.gdshader") } };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_stage);
        var scene = new TextureRect { Texture = texture, Size = new Vector2(1600, 900), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        _stage.AddChild(scene); _stage.AddChild(_decoration); _decoration.Draw += DrawDecoration;
        Place(Text(_stage, "✧ 星辰之旅", 32, new("f0dfb8"), 5), 55, 24, 400, 48);
        var online = Entry("Online", "", () => OnlineRequested?.Invoke());
        online.Icon = HtmlReliefStyle.Icon("online"); online.TooltipText = "联网冒险";
        online.AddThemeConstantOverride("icon_max_width", 22); Place(online, 1296, 32, 44, 44);
        _heroButton = Entry("Heroes", "", () => ShowChooser(true));
        _heroButton.Icon = HtmlReliefStyle.Icon("hero"); _heroButton.TooltipText = "英雄";
        _heroButton.AddThemeConstantOverride("icon_max_width", 22); Place(_heroButton, 1352, 32, 44, 44);
        var atlas = Entry("Atlas", "图鉴", () => AtlasRequested?.Invoke());
        atlas.Icon = GD.Load<Texture2D>("res://art/ui/lobby/book.svg"); atlas.AddThemeConstantOverride("icon_max_width", 16);
        atlas.Text = ""; atlas.TooltipText = "图鉴"; Place(atlas, 1408, 32, 44, 44);
        var settings = Entry("Settings", "", () => SettingsRequested?.Invoke()); settings.Icon = HtmlReliefStyle.Icon("settings"); settings.TooltipText = "设置"; settings.AddThemeConstantOverride("icon_max_width", 22); Place(settings, 1464, 32, 44, 44);
        _start = Entry("Start", "开始冒险    →", () => { if (!_start.Disabled) StartRequested?.Invoke(); });
        _start.AddThemeFontSizeOverride("font_size", 20);
        _start.AddThemeColorOverride("font_color", new Color("172823"));
        foreach (var state in new[] { "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            _start.AddThemeColorOverride(state, new Color("172823"));
        _start.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("c4a968"), new Color("d8c4a2")));
        _start.AddThemeStyleboxOverride("hover", MatchTheme.Surface(new Color("e1c98b"), new Color("eedfc4")));
        HtmlReliefStyle.Button(_start, gold: true); Place(_start, 1274, 799, 262, 65);
        _resume = Entry("Resume", "继续旅程 →", () => ResumeRequested?.Invoke());
        _resume.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); Place(_resume, 1100, 808, 158, 44); _resume.Hide();
        _portrait.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/CampPortrait.gdshader") };
        _stage.AddChild(_portrait); Place(_portrait, 64, 804, 65, 65);
        _stage.AddChild(_selectedName); MatchTheme.Text(_selectedName, 25, new Color("eadcc5"), spacing: 3);
        Place(_selectedName, 145, 830, 200, 38);
        _stage.AddChild(_selectedTitle); MatchTheme.Text(_selectedTitle, 14, new Color("b7a995")); Place(_selectedTitle, 350, 839, 160, 26);
        _heroDetails.AddRange([_portrait, _selectedName, _selectedTitle]);
        _stage.AddChild(_inspect); _inspect.AddChild(_info);
        var panel = MatchTheme.Surface(new Color("3b3229fa"), new Color("baa06b"));
        panel.ContentMarginLeft = panel.ContentMarginRight = panel.ContentMarginTop = panel.ContentMarginBottom = 24;
        panel.ShadowColor = new Color(0, 0, 0, .5f); panel.ShadowSize = 16; _inspect.AddThemeStyleboxOverride("panel", panel);
        _info.AddThemeConstantOverride("separation", 8); _inspect.Hide();
        BuildChooser();
        VisibilityChanged += () => { if (!IsVisibleInTree()) { HideInfo(); _chooser.Hide(); } };
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
                button.AddChild(plate); plate.AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color("251c16cc"), new Color("c8b47a66")));
                var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; labels.AddThemeConstantOverride("separation", 2); plate.AddChild(labels);
                var name = Text(labels, hero.Hero?.DisplayName ?? hero.Action.Text, 16, new("eadcc5"), 2); name.HorizontalAlignment = HorizontalAlignment.Center;
                var rect = new Rect2(camp.Rect.Position * new Vector2(1600, 900), camp.Rect.Size * new Vector2(1600, 900));
                button.Position = rect.Position; button.Size = rect.Size;
                var artSize = art.Texture.GetSize(); artSize *= Mathf.Min(rect.Size.X / artSize.X, rect.Size.Y / artSize.Y);
                art.Size = artSize; art.Position = new Vector2((rect.Size.X - artSize.X) / 2, rect.Size.Y - artSize.Y);
                plate.Position = new Vector2((rect.Size.X - 124) / 2, rect.Size.Y * 1.05f - 28); plate.Size = new Vector2(124, 28);
                button.Pressed += () => ShowChooser(true);
                button.MouseEntered += () => { art.Modulate = new Color(1.12f, 1.10f, 1.04f); ShowInfo(button); };
                button.MouseExited += () => { art.Modulate = Colors.White; HideInfo(); };
                button.FocusEntered += () => ShowInfo(button); button.FocusExited += HideInfo;
                button.GuiInput += input => Browse(button, input);
                _heroes.Add((hero, button, name));
            }
            _stage.MoveChild(_inspect, _stage.GetChildCount() - 1); BuildChoices();
            _stage.MoveChild(_chooser, _stage.GetChildCount() - 1);
        }
        if (!_heroes.Any(item => item.Hero.Key == SelectedHeroKey))
            SelectedHeroKey = _heroes.FirstOrDefault().Hero?.Key ?? new StringName("");
        UpdateSelection();
    }
    private void BuildChooser()
    {
        _stage.AddChild(_chooser); _chooser.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Name = "Backdrop", Color = Colors.White,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/HeroChooserBackdrop.gdshader") } };
        _chooser.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shade.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true }) ShowChooser(false); };
        _chooser.AddChild(_choicePanel); _choicePanel.AddThemeStyleboxOverride("panel", HtmlReliefStyle.Surface(margin: 24));
        HtmlReliefStyle.Shadow(_choicePanel);
        var column = new VBoxContainer { Name = "Content" }; column.AddThemeConstantOverride("separation", 20); _choicePanel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        var title = new Label { Text = "选择英雄", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        MatchTheme.Text(title, 18, new Color("eadcc5"), spacing: 2); header.AddChild(title);
        var close = new Button { Name = "Close", Icon = HtmlReliefStyle.Icon("close"), TooltipText = "关闭英雄选择", CustomMinimumSize = new Vector2(44, 44) };
        HtmlReliefStyle.Button(close); header.AddChild(close); close.Pressed += () => ShowChooser(false);
        column.AddChild(_choices); _choices.AddThemeConstantOverride("separation", 12);
    }
    private void BuildChoices()
    {
        foreach (var child in _choices.GetChildren()) { _choices.RemoveChild(child); child.QueueFree(); }
        _choiceButtons.Clear();
        foreach (var (hero, _, _) in _heroes)
        {
            var button = new Button { Name = Camp[hero.Key].Art, CustomMinimumSize = new Vector2(124, 260),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, ToggleMode = true, Disabled = !hero.Action.Enabled,
                TooltipText = hero.Hero?.DisplayName ?? hero.Action.Text };
            HtmlReliefStyle.Button(button); button.AddThemeStyleboxOverride("pressed", HtmlReliefStyle.Surface("selected"));
            _choices.AddChild(button);
            var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            content.OffsetLeft = content.OffsetTop = 1; content.OffsetRight = -1; content.OffsetBottom = -14;
            content.AddThemeConstantOverride("separation", 8);
            var art = new TextureRect { Texture = hero.Illustration.IsEmpty ? null : GD.Load<Texture2D>(hero.Illustration.ToString()),
                CustomMinimumSize = new Vector2(0, 180), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps }; content.AddChild(art);
            var name = Text(content, hero.Hero?.DisplayName ?? hero.Action.Text, 14, new Color("dfd1be")); name.HorizontalAlignment = HorizontalAlignment.Center;
            var caption = Text(content, hero.Hero?.Title ?? "", 11, new Color("b6a795")); caption.HorizontalAlignment = HorizontalAlignment.Center;
            var check = new Label { Name = "Selected", Text = "✓", Position = new Vector2(96, 8), Size = new Vector2(20, 20),
                MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
            MatchTheme.Text(check, 14, new Color("e1c48a")); button.AddChild(check);
            button.Pressed += () => { _hasChosenHero = true; SelectHero(hero.Key); ShowChooser(false); };
            button.GuiInput += input =>
            {
                if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
                var step = key.Keycode is Key.Left or Key.Up ? -1 : key.Keycode is Key.Right or Key.Down ? 1 : 0;
                if (step == 0) return;
                var index = _choiceButtons.FindIndex(item => item.Button == button);
                _choiceButtons[(index + step + _choiceButtons.Count) % _choiceButtons.Count].Button.GrabFocus(); button.AcceptEvent();
            };
            _choiceButtons.Add((hero, button));
        }
    }
    private void ShowChooser(bool visible)
    {
        HideInfo(); _chooser.Visible = visible;
        if (visible)
        {
            _choicePanel.Size = new Vector2(860, 0); _choicePanel.Position = (new Vector2(1600, 900) - _choicePanel.Size) / 2;
            _choiceButtons.FirstOrDefault(choice => choice.Hero.Key == SelectedHeroKey).Button?.GrabFocus();
        }
        else _heroButton.GrabFocus();
    }
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !_chooser.Visible) return;
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape }) { ShowChooser(false); GetViewport().SetInputAsHandled(); }
        else if (input is InputEventKey { Pressed: true, Keycode: Key.Tab } tab) MatchMenuView.TrapTab(_choicePanel, tab);
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
            item.Button.Visible = _hasChosenHero && selected;
            item.Name.Text = item.Hero.Hero?.DisplayName ?? item.Hero.Action.Text;
            item.Name.AddThemeColorOverride("font_color", new Color(selected ? "ffe4a2" : "eadcc5"));
        }
        foreach (var control in _heroDetails) control.Visible = _hasChosenHero;
        foreach (var choice in _choiceButtons)
        {
            var selected = choice.Hero.Key == SelectedHeroKey;
            choice.Button.SetPressedNoSignal(selected);
            choice.Button.GetNode<Label>("Selected").Modulate = selected ? Colors.White : Colors.Transparent;
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
        Text(_info, details.Title, 13, new("c5ad87"), 3); Text(_info, details.DisplayName, 32, new("eadcc5"), 3);
        _info.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
        foreach (var (label, value, attribute) in new[] { ("最大生命", details.MaxHealth.ToString(), GameAttributeKeys.MaxHealth),
            ("最大魔法", details.MaxMana.ToString(), GameAttributeKeys.MaxMana), ("魔法再生", $"{details.ManaRegen} / 秒", GameAttributeKeys.ManaRegen),
            ("每轮收入", details.Income.ToString(), GameAttributeKeys.Income) })
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; _info.AddChild(row);
            var color = AttributePalette.Find(attribute) ?? MatchTheme.Ink;
            var caption = Text(row, label, 15, color); caption.SizeFlagsHorizontal = SizeFlags.ExpandFill; Text(row, value, 21, color);
        }
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
    public void Open(bool canResume) { _resume.Visible = canResume; HideInfo(); _chooser.Hide(); Show(); LayoutScene(); _start.GrabFocus(); }
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
        if (_hasChosenHero && selected is not null)
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
        button.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("322b24dd"), new Color("9eacaa66")));
        button.AddThemeStyleboxOverride("hover", MatchTheme.Surface(new Color("5d5043"), new Color("c5ad87")));
        var focus = MatchTheme.Surface(Colors.Transparent, new Color("e6d8c3")); focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeFontSizeOverride("font_size", 15); button.AddThemeColorOverride("font_color", new Color("dfd1be"));
        HtmlReliefStyle.Button(button); button.Pressed += action; return button;
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
