using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 大卡面预览浮层；等级与孔位仅提交只读应用查询，关闭后归还原控件焦点。
public sealed partial class CardPreviewView : Control
{
    private readonly CardDisplayAdapter _adapter = new();
    private CardPreviewQuery? _query;
    private CardSnapshot? _source;
    private MatchSnapshot? _context;
    private MatchSnapshot? _enemy;
    private Control? _returnFocus;
    private PanelContainer _panel = null!;
    private ScrollContainer _rightScroll = null!;
    private VBoxContainer _visual = null!;
    private HBoxContainer _body = null!;
    private Control _artHost = null!;
    private CardPreviewArt _face = null!;
    private CardRelatedEffectsView _related = null!;
    private VBoxContainer _right = null!;
    private HBoxContainer _levels = null!;
    private VBoxContainer _gems = null!;
    private CardDetailsContent _details = null!;
    private Label _name = null!;
    private Label _meta = null!;
    private Button _close = null!;
    private int _level;
    private StringName?[] _sockets = Array.Empty<StringName?>();
    public CardSnapshot? DisplayedCard { get; private set; }

    public override void _Ready()
    {
        Hide(); MouseFilter = MouseFilterEnum.Stop;
        var shade = new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Stop,
            Material = new ShaderMaterial { Shader = new Shader { Code = """
shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
void fragment(){ COLOR=vec4(textureLod(screen_texture,SCREEN_UV,3.5).rgb*0.55,1.0); }
""" } } };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new PanelContainer { Name = "Panel" }; AddChild(_panel);
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, side is "left" or "right" ? 12 : 24);
        _panel.AddChild(margin); var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; margin.AddChild(column);
        _close = new Button { Name = "Close", Text = "×", TopLevel = true, ZAsRelative = false, ZIndex = 56, CustomMinimumSize = new Vector2(36, 36) }; column.AddChild(_close); _close.Pressed += Close;
        _body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ShrinkCenter }; _body.AddThemeConstantOverride("separation", 28); column.AddChild(_body);
        _visual = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _visual.AddThemeConstantOverride("separation", 14); var visual = _visual; _body.AddChild(visual);
        _name = new Label { Name = "Title", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_name, 25, MatchTheme.Gold, bold: true); visual.AddChild(_name);
        _artHost = new Control { Name = "Artwork" }; visual.AddChild(_artHost);
        _face = new CardPreviewArt { Name = "TiltCard" }; _artHost.AddChild(_face);
        var hint = new Label { Text = "按住卡面拖动 · 松开回正", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(hint, 12, MatchTheme.Muted); visual.AddChild(hint);
        _meta = new Label { Name = "Metadata", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_meta, 13, MatchTheme.Muted); visual.AddChild(_meta);
        var rightPanel = new PanelContainer { Name = "RulesPanel", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var rightPlate = MatchTheme.Surface(new Color("293139"), new Color("a88e6577")); rightPlate.SetCornerRadiusAll(10); rightPlate.ContentMarginLeft = rightPlate.ContentMarginRight = rightPlate.ContentMarginTop = rightPlate.ContentMarginBottom = 22; rightPanel.AddThemeStyleboxOverride("panel", rightPlate); _body.AddChild(rightPanel);
        _rightScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; var rightScroll = _rightScroll; rightPanel.AddChild(rightScroll);
        _right = new VBoxContainer { Name = "Options" }; _right.AddThemeConstantOverride("separation", 12); _right.AddThemeFontSizeOverride("font_size", 12); rightScroll.AddChild(_right);
        _related = new CardRelatedEffectsView { Name = "Related", SizeFlagsVertical = SizeFlags.ShrinkCenter }; _body.AddChild(_related);
        _right.AddChild(new Label { Text = "等级预览" }); _levels = new HBoxContainer { Name = "Levels" }; _right.AddChild(_levels);
        _right.AddChild(new Label { Text = "宝石预览" }); _gems = new VBoxContainer { Name = "Gems" }; _right.AddChild(_gems);
        var reset = new Button { Name = "Reset", Text = "还原当前卡牌" }; _right.AddChild(reset); reset.Pressed += Reset;
        _right.AddChild(new Label { Text = "仅预览 · 不改变当前卡牌" });
        _details = new CardDetailsContent { Name = "Details", RuleFontSize = 15 }; _right.AddChild(_details);
        Resized += LayoutPreview; _artHost.Resized += LayoutArt; _visual.Resized += LayoutArt;
    }

    // 与HTML一样按屏幕像素布局，避免游戏画布缩放改变预览大小。
    public void SetBounds(Vector2 logicalBounds)
    {
        var scale = Mathf.Max(.01f, GetViewport().GetStretchTransform().Scale.X);
        var pixels = logicalBounds * scale;
        var detailScale = Mathf.Max(1, Mathf.Min(pixels.X / 1600f, pixels.Y / 900f));
        Scale = Vector2.One * detailScale / scale;
        Size = pixels / detailScale;
        LayoutPreview();
    }

    // 查询由项目入口注入，视图不持有可变对局或内容目录。
    public void SetQuery(CardPreviewQuery query) => _query = query;
    // 正式目录只用于关联卡牌的只读效果展示。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries) => _related.SetEntries(entries);

    // 打开本次实例的只读预览，保留实际孔位与等级。
    public void Open(CardSnapshot card, MatchSnapshot? context, MatchSnapshot? enemy)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner(); _source = card; _context = context; _enemy = enemy;
        _level = card.Level; _sockets = card.GemSockets.Select(gem => gem?.Key).ToArray();
        RebuildOptions(); Show(); LayoutPreview(); RenderPreview(); _close.GrabFocus();
    }

    // Escape与关闭按钮归还焦点，不发送业务取消或交易意图。
    public void Close()
    {
        Hide(); _face.ResetTilt(); _source = null; DisplayedCard = null;
        if (_returnFocus is not null && GodotObject.IsInstanceValid(_returnFocus) && _returnFocus.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    private void Reset()
    {
        if (_source is null) return;
        _level = _source.Level; _sockets = _source.GemSockets.Select(gem => gem?.Key).ToArray(); RebuildOptions(); RenderPreview();
    }
    private void RebuildOptions()
    {
        foreach (var node in _levels.GetChildren().Concat(_gems.GetChildren())) { node.GetParent().RemoveChild(node); node.QueueFree(); }
        if (_source is null) return;
        foreach (var level in _query?.Levels(_source) ?? new[] { _source.Level })
        {
            var button = new Button { Name = "Level" + level, Text = level + "级", ToggleMode = true, ButtonPressed = level == _level,
                SizeFlagsHorizontal = SizeFlags.ExpandFill }; _levels.AddChild(button); LevelPresentation.Frame(button, level);
            button.Pressed += () => { _level = level; RenderPreview(); };
        }
        for (var index = 0; index < _sockets.Length; index++)
        {
            var socketIndex = index; var row = new HBoxContainer(); _gems.AddChild(row); row.AddChild(new Label { Text = "宝石 " + (index + 1) });
            var options = new OptionButton { Name = "Socket" + index, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Disabled = !_source.Abilities.Any(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active) }; row.AddChild(options); options.AddItem("空孔");
            var gems = _query?.Gems ?? Array.Empty<GemSnapshot>();
            for (var gemIndex = 0; gemIndex < gems.Count; gemIndex++) { options.AddItem(gems[gemIndex].DisplayName); if (gems[gemIndex].Key == _sockets[index]) options.Select(gemIndex + 1); }
            options.ItemSelected += selected => { _sockets[socketIndex] = selected == 0 ? null : gems[(int)selected - 1].Key; RenderPreview(); };
        }
        if (_sockets.Length == 0) _gems.AddChild(new Label { Text = "无宝石孔" });
    }
    private void RenderPreview()
    {
        if (_source is null) return;
        DisplayedCard = _query?.Preview(_source, _level, _sockets, _context, _enemy) ?? _source;
        _name.Text = DisplayedCard.DisplayName; _face.Render(_adapter.Build(DisplayedCard));
        var seconds = CardDisplayAdapter.CooldownSeconds(DisplayedCard);
        _meta.Text = $"{DisplayedCard.Level}级 · 价值 {DisplayedCard.Value}" + (DisplayedCard.Abilities.Any(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active) ? $" · 冷却 {seconds:0.##}秒" : "");
        foreach (var button in _levels.GetChildren().OfType<Button>()) button.SetPressedNoSignal(button.Text == _level + "级");
        _details.Render(DisplayedCard, Math.Max(1, _right.CustomMinimumSize.X), Math.Max(80, Size.Y - 500));
        _related.Render(DisplayedCard, Mathf.Max(230, (_panel.Size.X - 80) * .8f / 3), Math.Max(100, Size.Y - 160));
        (_details.FindChild("CardName", true, false) as CanvasItem)?.Hide(); _details.GetNode<Control>("Footer").Hide(); LayoutArt();
    }
    private void LayoutPreview()
    {
        var width = Mathf.Min(1400, Size.X - 64);
        _panel.Size = new Vector2(width, Mathf.Max(1, Size.Y - 48)); _panel.Position = (Size - _panel.Size) / 2;
        _right.CustomMinimumSize = new Vector2(Mathf.Max(236, (width - 80) / 3 - 44), 0);
        _close.Scale = GetGlobalTransform().Scale;
        _close.GlobalPosition = GetGlobalTransform() * new Vector2(Size.X - 64, 24);

        if (Visible && DisplayedCard is not null) RenderPreview();
    }
    private void LayoutArt()
    {
        if (DisplayedCard is null || _artHost.Size.X < 1) return;
        var ratio = (int)DisplayedCard.Size / 2f;
        var cardWidth = Mathf.Min(Mathf.Max(1, _visual.Size.X - 28) * .88f, Size.Y * .55f * ratio);
        var cardHeight = cardWidth / ratio;
        var minimum = new Vector2(0, cardHeight + 58);
        if (!_artHost.CustomMinimumSize.IsEqualApprox(minimum)) _artHost.CustomMinimumSize = minimum;
        var faceSize = new Vector2(cardWidth, cardHeight) * 1.35f;
        if (!_face.Size.IsEqualApprox(faceSize)) _face.Size = faceSize;
        _face.Position = (_artHost.Size - _face.Size) / 2;

    }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        var height = Mathf.Min(Mathf.Max(100, Size.Y - 164), _right.GetCombinedMinimumSize().Y);
        if (!Mathf.IsEqualApprox(_rightScroll.CustomMinimumSize.Y, height)) _rightScroll.CustomMinimumSize = new Vector2(0, height);
    }
    public override void _Input(InputEvent input)
    {
        if (!Visible || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Tab)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus is null || !IsAncestorOf(focus)) _close.GrabFocus();
            else { var next = key.ShiftPressed ? focus.FindPrevValidFocus() : focus.FindNextValidFocus();
                if (next is not null && IsAncestorOf(next)) next.GrabFocus(); else _close.GrabFocus(); }
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _ExitTree() { _close.Pressed -= Close; Resized -= LayoutPreview; _artHost.Resized -= LayoutArt; _visual.Resized -= LayoutArt; }
}
