using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 大幅原画、底部横向英雄名册与只读初始属性；确认后才提交英雄key（表现层）。
public sealed partial class HeroSelectionView : Control
{
    public event Action<StringName, long>? Selected;
    private readonly Label _heading = new() { Text = "选择你的英雄", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _rosterHeading = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ScrollContainer _roster = new() { Name = "Roster", VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly HBoxContainer _entries = new() { Name = "Entries", SizeFlagsHorizontal = SizeFlags.ExpandFill,
        Alignment = BoxContainer.AlignmentMode.Center };
    private readonly TextureRect _center = new() { Name = "CurrentPortrait", MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
    private readonly Label _title = new() { Name = "HeroTitle", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _name = new() { Name = "HeroName", MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
    private readonly Button _attributes = new() { Name = "Attributes", Text = "初始属性 ▾", ToggleMode = true, ButtonPressed = true };
    private readonly GridContainer _stats = new() { Name = "Stats", Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _otherAttributes = new() { Name = "OtherAttributes", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _hint = new() { Text = "方向键切换 · Enter 确认", HorizontalAlignment = HorizontalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _choose = new() { Name = "Choose", Text = "选择英雄" };
    private readonly AudioStreamPlayer _selectionVoice = new() { Name = "SelectionVoice", MaxPolyphony = 1, VolumeDb = -1f };
    private readonly RandomNumberGenerator _selectionVoiceRandom = new();
    private readonly List<(Button Button, Action Handler)> _buttons = new();
    private readonly List<Label> _values = new();
    private IReadOnlyList<KeyedAction> _heroes = Array.Empty<KeyedAction>();
    private int _index;
    private long _revision;
    private Rect2 _rosterRect, _detailsRect;
    private Tween? _portraitTween;
    private ShaderMaterial? _portraitMotion;
    private double _portraitMotionSeconds;
    private static readonly StringName AnimatedHeroKey = new("hero.paladin");
    private static readonly StringName MotionTimeParameter = new("motion_time");
    private static readonly StringName[] PaladinVoicePaths =
    [
        new("res://audio/voices/heroes/paladin-selection.mp3"),
        new("res://audio/voices/heroes/paladin-selection-glory.mp3"),
    ];
    private static readonly Dictionary<StringName, Texture2D> Thumbnails = new();

    // 建立控件与本地浏览事件，不创建对局。
    public override void _Ready()
    {
        foreach (var control in new Control[] { _heading, _rosterHeading, _roster, _center, _title, _name,
            _attributes, _stats, _otherAttributes, _hint, _message, _choose }) AddChild(control);
        AddChild(_selectionVoice);
        _selectionVoiceRandom.Randomize();
        _roster.AddChild(_entries); _entries.AddThemeConstantOverride("separation", 12);
        _heading.AddThemeFontSizeOverride("font_size", 20); _heading.AddThemeColorOverride("font_color", MatchTheme.Muted);
        _name.AddThemeFontSizeOverride("font_size", 44);
        _title.AddThemeFontSizeOverride("font_size", 18); _title.AddThemeColorOverride("font_color", MatchTheme.Gold);
        _hint.AddThemeFontSizeOverride("font_size", 16); _message.AddThemeFontSizeOverride("font_size", 16);
        _otherAttributes.AddThemeFontSizeOverride("font_size", 16);
        _stats.AddThemeConstantOverride("h_separation", 16); _stats.AddThemeConstantOverride("v_separation", 12);
        foreach (var label in new[] { "最大生命", "最大魔法", "每轮收入" })
        {
            var color = AttributePalette.Find(label == "最大生命" ? AttributePalette.Health : label == "最大魔法"
                ? Project_Star.Domain.Common.GameAttributeKeys.Mana : Project_Star.Domain.Common.GameAttributeKeys.Income)!.Value;
            var caption = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            caption.AddThemeColorOverride("font_color", color); _stats.AddChild(caption);
            var value = new Label { HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", 20); value.AddThemeColorOverride("font_color", color); _stats.AddChild(value); _values.Add(value);
        }
        MatchTheme.Accent(_choose);
        _choose.Pressed += Choose; _attributes.Toggled += ToggleAttributes; Resized += Layout;
        VisibilityChanged += RefreshPortraitProcessing;
        RebuildRoster(); UpdatePreview(); Layout();
    }

    // 装饰时钟只在可见的圣骑士原画上推进，不使用战斗或对局状态。
    public override void _Process(double delta)
    {
        if (_portraitMotion is null || _center.Material != _portraitMotion || !IsVisibleInTree()) return;
        _portraitMotionSeconds = (_portraitMotionSeconds + delta) % 8.0;
        _portraitMotion.SetShaderParameter(MotionTimeParameter, (float)_portraitMotionSeconds);
    }

    // 同页刷新保留浏览英雄；收到的身份、数值和操作条件都是只读副本。
    public void Render(string message, IReadOnlyList<KeyedAction> heroes, long revision = 0)
    {
        var key = _heroes.Count == 0 ? new StringName("") : _heroes[_index].Key;
        var visible = heroes.Where(hero => hero.Action.Visible).OrderBy(hero => FactionDisplayOrder.Rank(hero.Key))
            .ThenBy(hero => hero.Key.ToString(), StringComparer.Ordinal).ToArray();
        var changed = !_heroes.SequenceEqual(visible);
        _heroes = Array.AsReadOnly(visible);
        _index = Math.Max(0, Array.FindIndex(visible, hero => hero.Key == key));
        _revision = revision; _message.Text = message;
        if (!IsNodeReady()) return;
        if (changed) RebuildRoster();
        UpdatePreview(); Layout();
    }

    // 键盘只在当前选角页及其焦点范围内生效；Enter不会同时触发按钮默认事件。
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is not null && !IsAncestorOf(focus)) return;
        if (key.Keycode is Key.Left or Key.Up) Browse(-1);
        else if (key.Keycode is Key.Right or Key.Down) Browse(1);
        else if (key.Keycode is Key.Enter or Key.KpEnter) Choose();
        else return;
        GetViewport().SetInputAsHandled();
    }

    // 清理按钮订阅和原画过渡，避免过期界面继续提交。
    public override void _ExitTree()
    {
        _choose.Pressed -= Choose; _attributes.Toggled -= ToggleAttributes; Resized -= Layout;
        VisibilityChanged -= RefreshPortraitProcessing;
        _selectionVoice.Stop();
        SetProcess(false); _center.Material = null;
        _portraitMotion?.Dispose(); _portraitMotion = null;
        _portraitTween?.Kill(); ClearRoster();
    }

    private void ClearRoster()
    {
        foreach (var (button, handler) in _buttons)
        { button.Pressed -= handler; _entries.RemoveChild(button); button.QueueFree(); }
        _buttons.Clear();
    }

    private void RebuildRoster()
    {
        ClearRoster();
        for (var index = 0; index < _heroes.Count; index++)
        {
            var item = _heroes[index]; var captured = index;
            var button = new Button { Name = $"Hero{index}", CustomMinimumSize = new Vector2(132, 118),
                TooltipText = item.Action.Text };
            var row = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = row.OffsetTop = 8; row.OffsetRight = row.OffsetBottom = -8;
            row.AddThemeConstantOverride("separation", 0);
            row.AddChild(new TextureRect { Texture = Thumbnail(item), CustomMinimumSize = new Vector2(58, 58),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var caption = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(caption);
            var name = new Label { Text = item.Hero?.DisplayName ?? item.Action.Text.Replace("选择：", ""),
                ClipText = true, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 15); caption.AddChild(name);
            var title = new Label { Text = item.Hero?.Title ?? "", ClipText = true,
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            title.AddThemeFontSizeOverride("font_size", 11); title.AddThemeColorOverride("font_color", MatchTheme.Muted); caption.AddChild(title);
            button.AddChild(new Label { Name = "SelectedMark", Position = new Vector2(110, 2),
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore });
            Action handler = () => SelectPreview(captured);
            button.Pressed += handler; _buttons.Add((button, handler)); _entries.AddChild(button);
        }
    }

    private void Browse(int direction)
    {
        if (_heroes.Count == 0) return;
        SelectPreview((_index + direction + _heroes.Count) % _heroes.Count);
        _buttons[_index].Button.GrabFocus(); _roster.EnsureControlVisible(_buttons[_index].Button);
    }

    private void SelectPreview(int index)
    {
        if (!IsVisibleInTree() || index < 0 || index >= _heroes.Count) return;
        _selectionVoice.Stop();
        if (_heroes[index].Key == AnimatedHeroKey)
        {
            var voicePath = PaladinVoicePaths[_selectionVoiceRandom.RandiRange(0, PaladinVoicePaths.Length - 1)];
            _selectionVoice.Stream = GD.Load<AudioStream>(voicePath.ToString());
            if (_selectionVoice.Stream is not null) _selectionVoice.Play();
        }
        if (index == _index) return;
        _index = index; UpdatePreview();
        _portraitTween?.Kill(); _center.Modulate = new Color(1, 1, 1, .65f);
        _portraitTween = CreateTween(); _portraitTween.TweenProperty(_center, "modulate:a", 1f, .15);
    }

    private void Choose()
    {
        if (_heroes.Count > 0 && !_choose.Disabled && IsVisibleInTree()) Selected?.Invoke(_heroes[_index].Key, _revision);
    }

    private void UpdatePreview()
    {
        var selected = _heroes.Count == 0 ? null : _heroes[_index];
        if (selected?.Key != AnimatedHeroKey) _selectionVoice.Stop();
        var hero = selected?.Hero;
        _rosterHeading.Text = $"英雄名册  ·  {_heroes.Count} 位";
        _center.Texture = selected is null ? null : Load(selected);
        UpdatePortraitMotion(selected);
        _name.Text = hero?.DisplayName ?? selected?.Action.Text.Replace("选择：", "") ?? "暂无英雄";
        _title.Text = hero?.Title ?? "";
        _choose.Text = selected is null ? "暂无可选英雄" : $"以{_name.Text}开始冒险";
        _choose.Disabled = selected is null || !selected.Action.Enabled;
        _choose.TooltipText = selected?.Action.Reason ?? "";
        _attributes.Visible = hero is not null;
        _stats.Visible = _otherAttributes.Visible = hero is not null && _attributes.ButtonPressed;
        if (hero is not null)
        {
            _values[0].Text = hero.MaxHealth.ToString(); _values[1].Text = hero.MaxMana.ToString(); _values[2].Text = hero.Income.ToString();
            _otherAttributes.Text = $"初始等级 {hero.Level} · 魔法再生 {hero.ManaRegen}";
            _otherAttributes.TooltipText = $"初始魔法 {hero.Mana}\n初始护甲 {hero.Armor}\n生命再生 {hero.HealthRegen}";
        }
        for (var index = 0; index < _buttons.Count; index++)
        {
            var button = _buttons[index].Button; var current = index == _index;
            button.AddThemeStyleboxOverride("normal", MatchTheme.Surface(current ? new Color("294138") : MatchTheme.SurfaceColor,
                current ? MatchTheme.Blue : new Color("756847")));
            button.GetNode<Label>("SelectedMark").Text = current ? "✓" : "";
        }
        QueueRedraw();
    }

    private void UpdatePortraitMotion(KeyedAction? selected)
    {
        if (selected?.Key == AnimatedHeroKey && _center.Texture is not null)
        {
            _portraitMotion ??= new ShaderMaterial
            { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/PaladinPortrait.gdshader") };
            if (_center.Material != _portraitMotion)
            {
                _portraitMotionSeconds = 0;
                _portraitMotion.SetShaderParameter(MotionTimeParameter, 0f);
                _center.Material = _portraitMotion;
            }
        }
        else
        {
            _center.Material = null; _portraitMotionSeconds = 0;
        }
        RefreshPortraitProcessing();
    }

    private void RefreshPortraitProcessing()
    {
        if (!IsVisibleInTree()) _selectionVoice.Stop();
        SetProcess(IsVisibleInTree() && _portraitMotion is not null && _center.Material == _portraitMotion);
    }

    private void ToggleAttributes(bool expanded)
    {
        _attributes.Text = expanded ? "初始属性 ▾" : "初始属性 ▸";
        _stats.Visible = _otherAttributes.Visible = expanded && _heroes.Count > 0 && _heroes[_index].Hero is not null;
    }

    private void Layout()
    {
        var bodyHeight = Mathf.Max(1, Mathf.Min(540, Size.Y - 258));
        var detailsWidth = Mathf.Clamp(Size.X * .27f, 270, 330);
        const float gap = 64;
        var main = Mathf.Max(1, Mathf.Min(bodyHeight, Size.X - detailsWidth - gap - 64));
        var total = Mathf.Min(Size.X - 32, Mathf.Max(900, main + detailsWidth + gap));
        var left = (Size.X - total) / 2;
        var top = 52 + Mathf.Max(0, (Size.Y - 258 - bodyHeight) / 2);
        _heading.Position = new Vector2(left, top - 48); _heading.Size = new Vector2(total, 36);
        var rosterTop = top + bodyHeight + 20;
        _rosterRect = new Rect2(left, rosterTop, total, 154);
        _rosterHeading.Position = new Vector2(left + 8, rosterTop + 4); _rosterHeading.Size = new Vector2(total - 16, 24);
        _rosterHeading.AddThemeFontSizeOverride("font_size", 13);
        _rosterHeading.AddThemeColorOverride("font_color", MatchTheme.Muted);
        _roster.Position = new Vector2(left, rosterTop + 28); _roster.Size = new Vector2(total, 134);
        var artWidth = total - detailsWidth - gap;
        _center.Position = new Vector2(left + (artWidth - main) / 2, top + (bodyHeight - main) / 2); _center.Size = Vector2.One * main;
        var right = left + artWidth + gap;
        var infoTop = top + Mathf.Max(0, (bodyHeight - 354) / 2);
        _detailsRect = new Rect2(right, infoTop, detailsWidth, 354);
        var contentWidth = detailsWidth - 32;
        _title.Position = new Vector2(right + 16, infoTop + 8); _title.Size = new Vector2(contentWidth, 26);
        _name.Position = new Vector2(right + 16, infoTop + 38); _name.Size = new Vector2(contentWidth, 54);
        _attributes.Position = new Vector2(right + 16, infoTop + 106); _attributes.Size = new Vector2(contentWidth, 32);
        _stats.Position = new Vector2(right + 16, infoTop + 148); _stats.Size = new Vector2(contentWidth, 108);
        _otherAttributes.Position = new Vector2(right + 16, infoTop + 264); _otherAttributes.Size = new Vector2(contentWidth, 24);
        _choose.Position = new Vector2(right + 16, infoTop + 294); _choose.Size = new Vector2(contentWidth, 44);
        _hint.Position = new Vector2(right + 8, infoTop + 340); _hint.Size = new Vector2(detailsWidth - 16, 24);
        _message.Position = new Vector2(left, rosterTop + 164); _message.Size = new Vector2(total, 22);
        QueueRedraw();
    }

    // 分隔线复用项目主题，原画保持完整且不写入装饰。
    public override void _Draw()
    {
        DrawLine(_rosterRect.Position, _rosterRect.Position + new Vector2(_rosterRect.Size.X, 0), new Color(MatchTheme.Gold, .3f));
        DrawLine(_detailsRect.Position + new Vector2(16, 100), _detailsRect.Position + new Vector2(_detailsRect.Size.X - 16, 100),
            new Color(MatchTheme.Gold, .4f));
    }

    private static Texture2D? Load(KeyedAction hero) => !hero.Illustration.IsEmpty && ResourceLoader.Exists(hero.Illustration.ToString())
        ? GD.Load<Texture2D>(hero.Illustration.ToString()) : null;

    private static Texture2D? Thumbnail(KeyedAction hero)
    {
        if (Thumbnails.TryGetValue(hero.Illustration, out var cached)) return cached;
        var original = PlayerHeroPanel.PortraitTexture(hero.Key, hero.Illustration);
        if (original is null) return null;
        using var image = original.GetImage();
        image.Resize(104, Math.Max(1, image.GetHeight() * 104 / image.GetWidth()), Image.Interpolation.Lanczos);
        var thumbnail = ImageTexture.CreateFromImage(image); Thumbnails.Add(hero.Illustration, thumbnail);
        return thumbnail;
    }
}
