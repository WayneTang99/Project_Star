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
    private HBoxContainer _body = null!;
    private Control _artHost = null!;
    private Project_Star.Presentation.CardFace.CardFace _face = null!;
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
        var shade = new ColorRect { Color = new Color("06110fe8"), MouseFilter = MouseFilterEnum.Stop };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new PanelContainer { Name = "Panel" }; AddChild(_panel);
        _panel.AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color("15261f"), MatchTheme.Gold));
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        _panel.AddChild(margin); var column = new VBoxContainer(); margin.AddChild(column);
        _close = new Button { Name = "Close", Text = "关闭 ×", SizeFlagsHorizontal = SizeFlags.ShrinkEnd }; column.AddChild(_close); _close.Pressed += Close;
        _body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill }; _body.AddThemeConstantOverride("separation", 36); column.AddChild(_body);
        var visual = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _body.AddChild(visual);
        _name = new Label { Name = "Title", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_name, 28, MatchTheme.Gold, bold: true); visual.AddChild(_name);
        _artHost = new Control { Name = "Artwork", SizeFlagsVertical = SizeFlags.ExpandFill }; visual.AddChild(_artHost);
        _face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<Project_Star.Presentation.CardFace.CardFace>(); _artHost.AddChild(_face);
        _meta = new Label { Name = "Metadata", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_meta, 16, MatchTheme.Muted); visual.AddChild(_meta);
        _right = new VBoxContainer { Name = "Options" }; _right.AddThemeConstantOverride("separation", 12); _body.AddChild(_right);
        _right.AddChild(new Label { Text = "等级预览" }); _levels = new HBoxContainer { Name = "Levels" }; _right.AddChild(_levels);
        _right.AddChild(new Label { Text = "宝石预览" }); _gems = new VBoxContainer { Name = "Gems" }; _right.AddChild(_gems);
        var reset = new Button { Name = "Reset", Text = "还原当前卡牌" }; _right.AddChild(reset); reset.Pressed += Reset;
        _right.AddChild(new Label { Text = "仅预览 · 不改变当前卡牌" });
        _details = new CardDetailsContent { Name = "Details" }; _right.AddChild(_details);
        Resized += LayoutPreview; _artHost.Resized += LayoutArt;
    }

    // 查询由项目入口注入，视图不持有可变对局或内容目录。
    public void SetQuery(CardPreviewQuery query) => _query = query;

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
        Hide(); _source = null; DisplayedCard = null;
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
        _name.Text = DisplayedCard.DisplayName; _face.SetCard(_adapter.Build(DisplayedCard));
        var seconds = CardDisplayAdapter.CooldownSeconds(DisplayedCard);
        _meta.Text = $"{DisplayedCard.Level}级 · 价值 {DisplayedCard.Value}" + (DisplayedCard.Abilities.Any(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active) ? $" · 冷却 {seconds:0.##}秒" : "");
        foreach (var button in _levels.GetChildren().OfType<Button>()) button.SetPressedNoSignal(button.Text == _level + "级");
        _details.Render(DisplayedCard, Math.Max(1, _right.CustomMinimumSize.X), Math.Max(80, _panel.Size.Y - 530));
        (_details.FindChild("CardName", true, false) as CanvasItem)?.Hide(); _details.GetNode<Control>("Footer").Hide(); LayoutArt();
    }
    private void LayoutPreview()
    {
        _panel.Position = new Vector2(36, 28); _panel.Size = new Vector2(Math.Max(1, Size.X - 72), Math.Max(1, Size.Y - 56));
        _right.CustomMinimumSize = new Vector2(Mathf.Clamp(Size.X * .32f, 300, 440), 0);
        if (Visible && DisplayedCard is not null) RenderPreview();
    }
    private void LayoutArt()
    {
        if (DisplayedCard is null || _artHost.Size.X < 1 || _artHost.Size.Y < 1) return;
        var ratio = (int)DisplayedCard.Size / 2f;
        var height = Math.Min(_artHost.Size.Y - 24, (_artHost.Size.X - 24) / ratio);
        var scale = Math.Max(.1f, height / 260);
        _face.SetDisplaySize(new Vector2(260 * ratio, 260)); _face.Scale = Vector2.One * scale;
        _face.Position = (_artHost.Size - new Vector2(height * ratio, height)) / 2;
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
    public override void _ExitTree() { _close.Pressed -= Close; Resized -= LayoutPreview; _artHost.Resized -= LayoutArt; }
}
