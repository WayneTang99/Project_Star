using System;
using Godot;
using Project_Star.Presentation.CardFace;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 浮动详情不参与主界面布局，窗口缩放时限制在可见区域内。
public sealed partial class CardDetailsView : PanelContainer
{
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? SellRequested;
    public event Action? Closed;
    private CardDetailsContent _content = null!;
    private Button _sell = null!;
    private CardSnapshot? _selling;
    private Guid _matchId;
    private Button _close = null!;
    private Vector2 _bounds;
    public EntityId? CurrentCardId { get; private set; }
    public override void _Ready()
    {
        var column = new VBoxContainer { Name = "Content" }; AddChild(column);
        var plate = MatchTheme.Plate("tooltip"); plate.ContentMarginLeft = plate.ContentMarginRight = plate.ContentMarginTop = plate.ContentMarginBottom = 20;
        AddThemeStyleboxOverride("panel", plate);
        _close = new Button { Name = "Close", Text = "×", Alignment = HorizontalAlignment.Center, TopLevel = true, ZAsRelative = false, ZIndex = ZIndex + 1, Size = new Vector2(20, 20) }; AddChild(_close);
        _close.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); _close.AddThemeFontSizeOverride("font_size", 12);
        _close.Pressed += Close;
        _content = new CardDetailsContent { Name = "CardContent" }; column.AddChild(_content); Hide();
        _sell = new Button { Name = "Sell", Visible = false, ClipText = true }; column.AddChild(_sell);
        _sell.AddThemeFontSizeOverride("font_size", 11);
        _sell.Pressed += ConfirmSale;
    }

    // 展示完整卡牌语义，位置来自用户点击而非模型数据。
    public void ShowCard(CardSnapshot card, Vector2 position, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        var width = Mathf.Min(bounds.X - 24, bounds.X < 900 ? 290 : 330);
        _bounds = bounds;
        CurrentCardId = card.Id;
        _content.Render(card, width - 42, Mathf.Max(60, bounds.Y - 240), battle);
        Size = new Vector2(width, 0);
        _selling = null; _sell.Hide();
        Position = position + new Vector2(16, 0); Show(); ClampTo(bounds);
        Modulate = new Color(1, 1, 1, 0); CreateTween().TweenProperty(this, "modulate:a", 1f, .12);
    }

    // 详情紧邻卡牌，右侧不足时翻到左侧，最后限制在窗口内。
    public void ShowNear(CardSnapshot card, Rect2 anchor, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        ShowCard(card, new Vector2(anchor.End.X, anchor.Position.Y), bounds, battle);
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(this)) return;
            var left = anchor.End.X + 16;
            if (left + Size.X > bounds.X - 12) left = anchor.Position.X - Size.X - 16;
            Position = new Vector2(left, anchor.Position.Y); ClampTo(bounds);
        }).CallDeferred();
    }

    // 选中卡牌显示明确金额的出售确认；普通右键详情不提供出售。
    public void ShowSelected(CardSnapshot card, Guid matchId, UiAction sell, Vector2 position, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        ShowCard(card, position, bounds, battle); _selling = card; _matchId = matchId;
        _sell.Text = $"确认出售 {card.DisplayName}（{card.Level}级）· 回补 {card.Value}";
        _sell.Visible = sell.Visible; _sell.Disabled = !sell.Enabled; _sell.TooltipText = _sell.Text + "\n" + sell.Reason;
    }

    // 回放刷新时复用原位置与卡牌身份，避免详情因每帧Render而消失。
    public void RefreshCard(CardSnapshot card, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        if (!Visible || CurrentCardId != card.Id) return;
        var position = Position;
        ShowCard(card, position, bounds, battle);
    }

    public override void _ExitTree() { _sell.Pressed -= ConfirmSale; _close.Pressed -= Close; }
    private void Close() { Hide(); CurrentCardId = null; Closed?.Invoke(); }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        var height = GetCombinedMinimumSize().Y;
        if (!Mathf.IsEqualApprox(Size.Y, height)) Size = new Vector2(Size.X, height);
        ClampTo(_bounds);
        _close.GlobalPosition = GlobalPosition + new Vector2(Size.X - 23, 3);
    }
    private void ConfirmSale()
    {
        if (_selling is { } card && !_sell.Disabled && Visible)
            SellRequested?.Invoke(_matchId, card.Id, card.Level, card.Value);
    }

    // 只调整浮层几何，不触发数据刷新。
    public void ClampTo(Vector2 bounds)
    {
        _bounds = bounds;
        if (Size.X > bounds.X - 24) Size = new Vector2(Mathf.Max(1, bounds.X - 24), Size.Y);
        Position = new Vector2(Mathf.Clamp(Position.X, 12, Mathf.Max(12, bounds.X - Size.X - 12)),
            Mathf.Clamp(Position.Y, 12, Mathf.Max(12, bounds.Y - Size.Y - 12)));
    }
}
