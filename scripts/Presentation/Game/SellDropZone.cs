using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Game;

// 全屏拖拽路由按落点区分摆放、镶嵌、取消和出售；小提示随鼠标，不遮挡桌面。
public sealed partial class SellDropZone : Control
{
    public Func<BoardDragData, Result<int>>? PreviewRequested { get; set; }
    public event Action<BoardDragData, CardSnapshot>? DropRequested;
    internal BoardZoneView[] Boards { get; set; } = Array.Empty<BoardZoneView>();
    internal GemShopView? Gems { get; set; }
    internal Control? BenchToggle { get; set; }
    private MatchSnapshot? _player;
    private bool _enabled;
    private PanelContainer _feedback = null!;
    private Label _hint = null!;
    private Label _title = null!;
    private Label _value = null!;
    private RichTextLabel _effect = null!;
    private bool _selling;
    private Color _color;
    private BoardZoneView? _lastBoard;

    public override void _Ready()
    {
        Visible = false; MouseFilter = MouseFilterEnum.Stop;
        _feedback = new PanelContainer { Name = "Feedback", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_feedback);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4); _feedback.AddChild(column);
        _title = new Label { Name = "CardName", MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        MatchTheme.Text(_title, 13, MatchTheme.Gold, bold: true); column.AddChild(_title);
        _value = new Label { Name = "Value", MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(_value, 16, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Value), serif: true); column.AddChild(_value);
        _effect = new RichTextLabel { Name = "SaleEffect", FitContent = true, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore };
        _effect.AddThemeFontSizeOverride("normal_font_size", 12); column.AddChild(_effect);
        _hint = new Label { Name = "Hint", MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        MatchTheme.Text(_hint, 11, MatchTheme.Muted); column.AddChild(_hint);
    }

    // 同批快照刷新不会提交任何拖拽操作。
    public void Render(MatchSnapshot? player, bool enabled)
    {
        _player = player; _enabled = enabled;
        if (!_enabled || _player is null) Hide();
    }

    private CardSnapshot? FindCard(Variant data)
    {
        if (!_enabled || _player is null || data.VariantType != Variant.Type.Object
            || data.AsGodotObject() is not BoardDragData drag || drag.MatchId != _player.MatchId
            || !_player.BoardPlacements.Any(placement => placement.CardId == drag.CardId)) return null;
        return _player.Cards.FirstOrDefault(card => card.Id == drag.CardId);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var card = FindCard(data); _selling = false;
        if (card is null || PreviewRequested is null) return false;
        var point = GlobalPosition + atPosition * GetGlobalTransform().Scale;
        var board = Boards.FirstOrDefault(b => b.IsVisibleInTree() && b.GetGlobalRect().Grow(24 * GetGlobalTransform().Scale.X).HasPoint(point));
        if (_lastBoard != board) _lastBoard?.ClearDropPreview();
        _lastBoard = board;
        var gem = Gems?.IsVisibleInTree() == true ? Gems.TargetAt(point) : null;
        bool valid;
        if (board is not null)
        {
            valid = board.GetGlobalRect().HasPoint(point) && board._CanDropData(board.GetGlobalTransform().AffineInverse() * point, data);
            _hint.Text = valid ? "松开摆放 · 青色区域为接收位置" : "此处无法摆放 · 松开取消，移出边缘后出售";
            _color = new Color(valid ? "83edf2" : "ef9585");
        }
        else if (gem is not null)
        {
            valid = gem._CanDropData(gem.GetGlobalTransform().AffineInverse() * point, data);
            _hint.Text = valid ? "松开镶嵌" : gem.DropHint + " · 松开取消";
            _color = new Color(valid ? "d0a0f3" : "ef9585");
        }
        else if (BenchToggle?.GetGlobalRect().HasPoint(point) == true)
        {
            valid = false; _hint.Text = "停留打开备战 · 松开取消"; _color = new Color("a2a9b3");
        }
        else
        {
            var result = PreviewRequested((BoardDragData)data.AsGodotObject());
            valid = result.IsSuccess && result.Value == card.Value;
            _selling = valid; _hint.Text = valid ? "松开出售 · Escape取消" : result.Failure?.Message ?? "卡牌信息已变化";
            _color = new Color(valid ? "efc56f" : "ef9585");
        }
        _title.Text = card.DisplayName + (board is not null ? " · 摆放" : gem is not null ? " · 镶嵌" : _selling ? " · 出售" : " · 取消");
        _value.Text = $"出售可得 {card.Value} 金币"; _value.Visible = _selling;
        CardKeywordText.RenderRule(_effect, string.Join(" · ", card.DescriptionEntries.Where(e => e.KeywordKey == CardKeywords.Sell).Select(e => e.Text)));
        _effect.Visible = _selling && _effect.GetParsedText().Length > 0;
        var plate = MatchTheme.Surface(new Color("3f3329f5"), _color); plate.SetCornerRadiusAll(7);
        plate.ContentMarginLeft = plate.ContentMarginRight = 14; plate.ContentMarginTop = plate.ContentMarginBottom = 12;
        _feedback.AddThemeStyleboxOverride("panel", plate);
        _feedback.Size = new Vector2(250, 0);
        var offset = atPosition + new Vector2(22, 24);
        _feedback.Position = new Vector2(Mathf.Clamp(offset.X, 8, Math.Max(8, Size.X - 258)), Mathf.Clamp(offset.Y, 8, Math.Max(8, Size.Y - _feedback.Size.Y - 8)));
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) return;
        var point = GlobalPosition + atPosition * GetGlobalTransform().Scale;
        if (_lastBoard is not null) _lastBoard._DropData(_lastBoard.GetGlobalTransform().AffineInverse() * point, data);
        else if (Gems?.IsVisibleInTree() == true && Gems.TargetAt(point) is { } gem) gem._DropData(gem.GetGlobalTransform().AffineInverse() * point, data);
        else if (_selling) DropRequested?.Invoke((BoardDragData)data.AsGodotObject(), FindCard(data)!);
        Hide();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && _hint is not null && FindCard(GetViewport().GuiGetDragData()) is not null)
        { Show(); _CanDropData(GetLocalMousePosition(), GetViewport().GuiGetDragData()); }
        if (what == NotificationDragEnd) { _lastBoard?.ClearDropPreview(); _lastBoard = null; Hide(); }
    }

    public override void _Process(double delta)
    { if (Visible && GetViewport().GuiIsDragging()) _CanDropData(GetLocalMousePosition(), GetViewport().GuiGetDragData()); }
}
