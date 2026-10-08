using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 商店商品不重复卡名和等级，购买意图携带报价版本，说明保留于提示和详情。
public sealed partial class ShopView : Control
{
    public event Action<int, long>? BuyRequested;
    public event Action<Project_Star.Application.Match.CardSnapshot>? DetailsRequested;
    private Label _message = null!;
    private ScrollContainer _scroll = null!;
    private HBoxContainer _offers = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private readonly List<(Control Row, CardItemView Card, int Slots, VBoxContainer Info, VBoxContainer Actions, Action Layout)> _items = new();
    private readonly CardDisplayAdapter _adapter = new();

    public override void _Ready()
    {
        _message = new Label { Name = "Message", ClipText = true, Visible = false };
        _message.AddThemeFontSizeOverride("font_size", 12); AddChild(_message);
        _scroll = new ScrollContainer { Name = "OfferScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever }; AddChild(_scroll);
        _offers = new HBoxContainer { Name = "Offers", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(_offers);
        Resized += LayoutView; LayoutView();
    }

    // 价格与合并目标等级由页面模型传入，界面不执行购买或合并。
    public void Render(string message, IReadOnlyList<ShopItemViewModel> offers, int shopLevel = 0)
    {
        Clear(); _message.Text = message; _message.TooltipText = message;
        TooltipText = $"{shopLevel}级商店 · 右键查看商品详情";
        // 常态操作说明放在提示中；失败、购买和刷新反馈保留在内容底部。
        _message.Visible = message.Length > 0 && !message.StartsWith("选择") && !message.StartsWith("商店")
            && !message.StartsWith("购买卡牌");
        foreach (var offer in offers)
        {
            var row = new Control { Name = $"Offer{offer.Index}", Visible = offer.Action.Visible,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            _offers.AddChild(row);
            var card = new CardItemView { Name = "Card" };
            row.AddChild(card); card.Render(offer.Card, _adapter); card.DetailsRequested += ShowDetails;
            var info = new VBoxContainer { Name = "Info" }; row.AddChild(info);
            if (offer.Card.CurrentValues.TryGetValue(GameAttributeKeys.CooldownTicks, out var ticks) && ticks > 0)
                info.AddChild(MatchTheme.Stat("clock", $"{ticks / 10m * offer.Card.CooldownMultiplier:0.##}s", "当前冷却"));
            if (offer.MergeLevel > 0)
                info.AddChild(MatchTheme.Stat("merge", $"{offer.Card.Level} → {offer.MergeLevel}", "购买后与已有同级卡牌合并"));
            var actions = new VBoxContainer { Name = "Actions" }; row.AddChild(actions);
            var sold = offer.Action.Text == "已售罄";
            var button = new Button { Name = "Buy", ClipText = true,
                Text = sold ? "已售罄" : $"购买  {offer.Price}", Icon = sold ? null : MatchTheme.Icon("coin"),
                Disabled = !offer.Action.Enabled,
                CustomMinimumSize = new Vector2(100, 32),
                TooltipText = CardDisplayAdapter.Details(offer.Card) + $"\n购买价 {offer.Price} · 持有价值 {offer.Card.Value}\n" + offer.Action.Reason };
            button.AddThemeConstantOverride("icon_max_width", 14);
            button.AddThemeFontSizeOverride("font_size", 14);
            Action handler = () => { if (!button.Disabled && button.Visible) BuyRequested?.Invoke(offer.Index, offer.Revision); };
            actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
            var reason = new Label { Text = offer.Action.Reason, Visible = offer.Action.Reason.Length > 0,
                ClipText = true, TooltipText = offer.Action.Reason };
            reason.AddThemeFontSizeOverride("font_size", 11); actions.AddChild(reason);
            var slots = (int)offer.Card.Size;
            Action layout = () => LayoutOffer(row, card, slots, info, actions);
            row.Resized += layout; _items.Add((row, card, slots, info, actions, layout));
            card.Modulate = sold ? new Color(1, 1, 1, .45f) : Colors.White;
        }
        LayoutView();
    }

    public override void _ExitTree() { Resized -= LayoutView; Clear(); }

    private void Clear()
    {
        foreach (var (button, handler) in _bindings) button.Pressed -= handler;
        foreach (var item in _items)
        {
            item.Card.DetailsRequested -= ShowDetails; item.Row.Resized -= item.Layout;
            _offers.RemoveChild(item.Row); item.Row.QueueFree();
        }
        _items.Clear(); _bindings.Clear();
    }

    private void ShowDetails(Project_Star.Application.Match.CardSnapshot card) => DetailsRequested?.Invoke(card);

    private void LayoutView()
    {
        var feedbackHeight = _message.Visible ? 20 : 0;
        _message.Position = new Vector2(8, Size.Y - feedbackHeight);
        _message.Size = new Vector2(Mathf.Max(1, Size.X - 16), feedbackHeight);
        _scroll.Position = new Vector2(8, 4); _scroll.Size = new Vector2(Mathf.Max(1, Size.X - 16), Mathf.Max(1, Size.Y - 8 - feedbackHeight));
        _offers.CustomMinimumSize = new Vector2(0, _scroll.Size.Y);
        foreach (var item in _items)
        {
            item.Row.CustomMinimumSize = new Vector2(100, _scroll.Size.Y);
            item.Layout();
        }
    }

    private void LayoutOffer(Control row, CardItemView card, int slots, VBoxContainer info, VBoxContainer actions)
    {
        var column = Mathf.Max(100, (_scroll.Size.X - Math.Max(0, _items.Count - 1) * MatchTheme.Gap) / Math.Max(1, _items.Count));
        var largest = _items.Count == 0 ? slots : _items.Max(item => item.Slots);
        var height = Mathf.Max(40, Mathf.Min(_scroll.Size.Y - 54, (column - 76) * 2 / largest));
        card.Position = new Vector2(4, 0); card.Size = new Vector2(height * slots / 2, height);
        var sideInfo = card.Size.X + 64 < column;
        info.Position = new Vector2(card.Size.X + 14, height * .25f);
        info.Size = new Vector2(Mathf.Max(1, column - info.Position.X - 4), sideInfo ? height * .6f : 20);
        info.Visible = sideInfo;
        // 三尺寸共享按钮基线，禁用原因与操作反馈各留独立空间。
        actions.Position = new Vector2(4, _scroll.Size.Y - 52);
        actions.Size = new Vector2(Mathf.Max(100, column - 8), 52);
    }
}
