using System;
using Godot;
using Project_Star.Presentation.CardFace;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 浮动详情不参与主界面布局，窗口缩放时限制在可见区域内。
public sealed partial class CardDetailsView : PanelContainer
{
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? SellRequested;
    private RichTextLabel _text = null!;
    private Button _sell = null!;
    private CardSnapshot? _selling;
    private Guid _matchId;
    public override void _Ready()
    {
        var column = new VBoxContainer { Name = "Content" }; AddChild(column);
        var close = new Button { Text = "关闭详情", Alignment = HorizontalAlignment.Right }; column.AddChild(close);
        close.Pressed += Hide;
        _text = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_text); Hide();
        _sell = new Button { Name = "Sell", Visible = false, ClipText = true }; column.AddChild(_sell);
        _sell.Pressed += ConfirmSale;
    }

    // 展示完整卡牌语义，位置来自用户点击而非模型数据。
    public void ShowCard(CardSnapshot card, Vector2 position, Vector2 bounds)
    {
        CardKeywordText.RenderDetails(_text, CardDisplayAdapter.Details(card));
        _selling = null; _sell.Hide();
        Position = position; Show(); ClampTo(bounds);
    }

    // 选中卡牌显示明确金额的出售确认；普通右键详情不提供出售。
    public void ShowSelected(CardSnapshot card, Guid matchId, UiAction sell, Vector2 position, Vector2 bounds)
    {
        ShowCard(card, position, bounds); _selling = card; _matchId = matchId;
        _sell.Text = $"确认出售 {card.DisplayName}（{card.Level}级）· 回补 {card.Value}";
        _sell.Visible = sell.Visible; _sell.Disabled = !sell.Enabled; _sell.TooltipText = _sell.Text + "\n" + sell.Reason;
    }

    public override void _ExitTree() => _sell.Pressed -= ConfirmSale;
    private void ConfirmSale()
    {
        if (_selling is { } card && !_sell.Disabled && Visible)
            SellRequested?.Invoke(_matchId, card.Id, card.Level, card.Value);
    }

    // 只调整浮层几何，不触发数据刷新。
    public void ClampTo(Vector2 bounds)
    {
        Size = new Vector2(Mathf.Min(460, bounds.X), Mathf.Min(480, bounds.Y));
        Position = new Vector2(Mathf.Clamp(Position.X, 0, Mathf.Max(0, bounds.X - Size.X)),
            Mathf.Clamp(Position.Y, 0, Mathf.Max(0, bounds.Y - Size.Y)));
    }
}
