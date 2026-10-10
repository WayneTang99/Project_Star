using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 英雄名册、完整原画和只读初始属性；确认后才提交英雄key（表现层）。
public sealed partial class HeroSelectionView : Control
{
    public event Action<StringName, long>? Selected;
    private readonly Label _heading = new() { Text = "选择你的英雄", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _rosterHeading = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ScrollContainer _roster = new() { Name = "Roster", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly VBoxContainer _entries = new() { Name = "Entries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
    private static readonly StringName PaladinVoicePath = new("res://audio/voices/heroes/paladin-selection.mp3");
    private static readonly Dictionary<StringName, Texture2D> Thumbnails = new();

    // 建立控件与本地浏览事件，不创建对局。
    public override void _Ready()
    {
        foreach (var control in new Control[] { _heading, _rosterHeading, _roster, _center, _title, _name,
            _attributes, _stats, _otherAttributes, _hint, _message, _choose }) AddChild(control);
        AddChild(_selectionVoice);
        _roster.AddChild(_entries); _entries.AddThemeConstantOverride("separation", 6);
        _heading.AddThemeFontSizeOverride("font_size", 24); _name.AddThemeFontSizeOverride("font_size", 36);
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
        var visible = heroes.Where(hero => hero.Action.Visible).ToArray();
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
            var button = new Button { Name = $"Hero{index}", CustomMinimumSize = new Vector2(0, 72),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = item.Action.Text };
            var row = new HBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = row.OffsetTop = 8; row.OffsetRight = row.OffsetBottom = -8;
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new TextureRect { Texture = Thumbnail(item), CustomMinimumSize = new Vector2(52, 52),
                MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var caption = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(caption);
            var name = new Label { Text = item.Hero?.DisplayName ?? item.Action.Text.Replace("选择：", ""),
                ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 18); caption.AddChild(name);
            var title = new Label { Text = item.Hero?.Title ?? "", ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
            title.AddThemeFontSizeOverride("font_size", 16); caption.AddChild(title);
            row.AddChild(new Label { Name = "SelectedMark", CustomMinimumSize = new Vector2(18, 0),
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
            _selectionVoice.Stream ??= GD.Load<AudioStream>(PaladinVoicePath.ToString());
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
            _otherAttributes.Text = $"初始等级 {hero.Level}\n初始魔法 {hero.Mana} · 魔法再生 {hero.ManaRegen}";
            _otherAttributes.TooltipText = $"初始护甲 {hero.Armor}\n生命再生 {hero.HealthRegen}";
        }
        for (var index = 0; index < _buttons.Count; index++)
        {
            var button = _buttons[index].Button; var current = index == _index;
            button.AddThemeStyleboxOverride("normal", MatchTheme.Surface(current ? new Color("294138") : MatchTheme.SurfaceColor,
                current ? MatchTheme.Blue : new Color("756847")));
            button.GetNode<Label>("Content/SelectedMark").Text = current ? "✓" : "";
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
        var bodyHeight = Mathf.Max(1, Size.Y - 84);
        var rosterWidth = Mathf.Clamp(Size.X * .19f, 190, 250);
        var detailsWidth = Mathf.Clamp(Size.X * .25f, 250, 300);
        const float gap = 24;
        var main = Mathf.Max(1, Mathf.Min(bodyHeight - 12, Size.X - rosterWidth - detailsWidth - gap * 2 - 32));
        var total = rosterWidth + detailsWidth + main + gap * 2;
        var left = (Size.X - total) / 2; const float top = 48;
        _heading.Position = new Vector2(left, 4); _heading.Size = new Vector2(total, 36);
        _rosterRect = new Rect2(left, top, rosterWidth, bodyHeight);
        _rosterHeading.Position = new Vector2(left + 12, top + 8); _rosterHeading.Size = new Vector2(rosterWidth - 24, 28);
        _roster.Position = new Vector2(left + 8, top + 44); _roster.Size = new Vector2(rosterWidth - 16, bodyHeight - 52);
        _center.Position = new Vector2(left + rosterWidth + gap, top + (bodyHeight - main) / 2); _center.Size = Vector2.One * main;
        var right = left + rosterWidth + gap + main + gap;
        _detailsRect = new Rect2(right, top, detailsWidth, bodyHeight);
        var contentWidth = detailsWidth - 32;
        _title.Position = new Vector2(right + 16, top + 20); _title.Size = new Vector2(contentWidth, 26);
        _name.Position = new Vector2(right + 16, top + 48); _name.Size = new Vector2(contentWidth, 54);
        _attributes.Position = new Vector2(right + 16, top + 122); _attributes.Size = new Vector2(contentWidth, 32);
        _stats.Position = new Vector2(right + 16, top + 172); _stats.Size = new Vector2(contentWidth, 116);
        _otherAttributes.Position = new Vector2(right + 16, top + 310); _otherAttributes.Size = new Vector2(contentWidth, 52);
        _choose.Position = new Vector2(right + 16, top + bodyHeight - 92); _choose.Size = new Vector2(contentWidth, 48);
        _hint.Position = new Vector2(right + 8, top + bodyHeight - 36); _hint.Size = new Vector2(detailsWidth - 16, 24);
        _message.Position = new Vector2(left, top + bodyHeight + 6); _message.Size = new Vector2(total, 30);
        QueueRedraw();
    }

    // 面板和原画边框复用项目主题；不把装饰写入图片。
    public override void _Draw()
    {
        MatchTheme.DrawSurface(this, _rosterRect); MatchTheme.DrawSurface(this, _detailsRect);
        MatchTheme.DrawSurface(this, new Rect2(_center.Position - Vector2.One * 5, _center.Size + Vector2.One * 10));
        DrawLine(_detailsRect.Position + new Vector2(16, 110), _detailsRect.Position + new Vector2(_detailsRect.Size.X - 16, 110),
            new Color(MatchTheme.Gold, .4f));
    }

    private static Texture2D? Load(KeyedAction hero) => !hero.Illustration.IsEmpty && ResourceLoader.Exists(hero.Illustration.ToString())
        ? GD.Load<Texture2D>(hero.Illustration.ToString()) : null;

    private static Texture2D? Thumbnail(KeyedAction hero)
    {
        if (Thumbnails.TryGetValue(hero.Illustration, out var cached)) return cached;
        var original = Load(hero);
        if (original is null) return null;
        using var image = original.GetImage();
        image.Resize(104, Math.Max(1, image.GetHeight() * 104 / image.GetWidth()), Image.Interpolation.Lanczos);
        var thumbnail = ImageTexture.CreateFromImage(image); Thumbnails.Add(hero.Illustration, thumbnail);
        return thumbnail;
    }
}
