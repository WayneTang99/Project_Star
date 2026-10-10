using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Playtest;

// 上方出售目标仅在己方卡牌拖拽期间显示，通过意图调用应用出售用例。
public sealed partial class SellDropZone : PanelContainer
{
    public Func<BoardDragData, Result<int>>? PreviewRequested { get; set; }
    public event Action<BoardDragData, CardSnapshot>? DropRequested;
    private MatchSnapshot? _player;
    private bool _enabled;
    private Label _hint = null!;
    private Label _title = null!;
    private Label _value = null!;
    private RichTextLabel _effect = null!;
    private bool _valid;
    private bool _over;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty { ContentMarginLeft = 22, ContentMarginRight = 22,
            ContentMarginTop = 18, ContentMarginBottom = 18 });
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 7); AddChild(column);
        _title = new Label { Name = "CardName", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(_title, 20, MatchTheme.Gold, bold: true); column.AddChild(_title);
        _value = new Label { Name = "Value", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(_value, 24, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Value), serif: true); column.AddChild(_value);
        _effect = new RichTextLabel { Name = "SaleEffect", FitContent = true, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore };
        _effect.AddThemeFontSizeOverride("normal_font_size", 12); column.AddChild(_effect);
        _hint = new Label { Name = "Hint", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        MatchTheme.Text(_hint, 12, MatchTheme.Muted); column.AddChild(_hint);
    }

    public void Render(MatchSnapshot? player, bool enabled)
    {
        _player = player;
        _enabled = enabled;
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
        var card = FindCard(data);
        if (card is null || PreviewRequested is null) return false;
        var result = PreviewRequested((BoardDragData)data.AsGodotObject());
        var valid = result.IsSuccess && result.Value == card.Value;
        _valid = valid;
        _title.Text = $"✧　{card.DisplayName} · {card.Level}级";
        _title.AddThemeColorOverride("font_color", Project_Star.Presentation.CardFace.CardLevelGem.LevelColor(card.Level));
        _value.Text = $"出售获得  {card.Value} 金币";
        CardKeywordText.RenderRule(_effect, string.Join(" · ", card.DescriptionEntries.Where(entry => entry.KeywordKey == CardKeywords.Sell).Select(entry => entry.Text)));
        _effect.Visible = _effect.GetParsedText().Length > 0;
        _hint.Text = valid ? "移入区域松开出售 · 移出或Escape取消"
            : result.Failure?.Message ?? "卡牌信息已变化，请重新拖拽。";
        QueueRedraw();
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) return;
        var card = FindCard(data)!;
        Hide();
        DropRequested?.Invoke((BoardDragData)data.AsGodotObject(), card);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && _hint is not null)
        {
            var data = GetViewport().GuiGetDragData();
            if (FindCard(data) is null) return;
            _CanDropData(Vector2.Zero, data);
            Show();
        }
        if (what == NotificationDragEnd) Hide();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        var over = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        if (_over == over) return;
        _over = over; QueueRedraw();
    }
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        MatchTheme.DrawSurface(this, rect, "stage");
        var color = _over && _valid ? AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Value)!.Value : MatchTheme.Gold;
        // 低对比斜纹及中心星纹保留主题纹理，不用纯色遮住整片界面。
        DrawRect(rect.Grow(-8), new Color(color, .35f), false, 1);
        for (var x = -Size.Y; x < Size.X; x += 22)
        {
            var start = new Vector2(Mathf.Max(0, x), Mathf.Max(0, -x));
            var end = new Vector2(Mathf.Min(Size.X, x + Size.Y), Mathf.Min(Size.Y, Size.X - x));
            DrawLine(start, end, new Color(color, .04f), 1);
        }
        DrawCircle(Size / 2, 62, new Color(color, _over && _valid ? .12f : .04f));
        DrawStyleBox(MatchTheme.Outline(new Color(color, _over && _valid ? 1 : .55f)), rect);
    }
}
