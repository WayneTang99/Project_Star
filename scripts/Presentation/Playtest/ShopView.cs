using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 商品卡面保留原画，名称与冷却显示在侧边；购买意图携带报价版本。
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
        _message.AddThemeFontSizeOverride("font_size", 16); AddChild(_message);
        _scroll = new ScrollContainer { Name = "OfferScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever }; AddChild(_scroll);
        _offers = new HBoxContainer { Name = "Offers", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _scroll.AddChild(_offers);
        Resized += LayoutView; LayoutView();
    }

    // 价格与合并目标等级由页面模型传入，界面不执行购买或合并。
    public void Render(string message, IReadOnlyList<ShopItemViewModel> offers, int shopLevel = 0)
    {
        Clear(); _message.Text = message; _message.TooltipText = message;
        TooltipText = $"{shopLevel}级商店 · 右键查看商品详情";
        // 常态操作说明放在提示中；失败、购买和刷新反馈保留在内容底部。
        _message.Hide();
        foreach (var offer in offers)
        {
            var row = new Control { Name = $"Offer{offer.Index}", Visible = offer.Action.Visible,
                MouseFilter = MouseFilterEnum.Ignore };
            _offers.AddChild(row);
            var card = new CardItemView { Name = "Card" };
            row.AddChild(card); card.Render(offer.Card, _adapter); card.DetailsRequested += ShowDetails;
            var info = new VBoxContainer { Name = "Info" }; row.AddChild(info);
            var name = new Label { Text = offer.Card.DisplayName, AutowrapMode = TextServer.AutowrapMode.WordSmart, TooltipText = offer.Card.DisplayName };
            MatchTheme.Text(name, 12, new Color("e0dec9"));
            info.AddThemeConstantOverride("separation", 10);
            info.AddChild(name);
            if (offer.Card.CurrentValues.TryGetValue(GameAttributeKeys.CooldownTicks, out var ticks) && ticks > 0)
                info.AddChild(MatchTheme.Stat("clock", $"{ticks / 10m * offer.Card.CooldownMultiplier:0.##}s", "当前冷却"));
            if (offer.MergeLevel > 0)
            {
                var merge = MatchTheme.Stat("merge", $"{offer.Card.Level} → {offer.MergeLevel}", "购买后与已有同级卡牌合并");
                merge.Name = "MergePreview"; info.AddChild(merge);
            }
            var actions = new VBoxContainer { Name = "Actions" }; row.AddChild(actions);
            var sold = offer.Action.Text == "已售罄";
            var button = new Button { Name = "Buy", ClipText = true,
                Text = sold ? "已售罄" : offer.Price.ToString(), Icon = sold ? null : MatchTheme.Icon("coin"),
                Disabled = !offer.Action.Enabled,
                CustomMinimumSize = new Vector2(65, 32),
                TooltipText = CardDisplayAdapter.Details(offer.Card) + $"\n购买价 {offer.Price} · 持有价值 {offer.Card.Value}\n"
                    + (offer.MergeLevel > 0 ? $"可合并至 {offer.MergeLevel}级\n" : "") + offer.Action.Reason };
            button.AddThemeConstantOverride("icon_max_width", 14);
            button.AddThemeFontSizeOverride("font_size", 12);
            MatchTheme.Accent(button);
            Action handler = () => { if (!button.Disabled && button.Visible) BuyRequested?.Invoke(offer.Index, offer.Revision); };
            actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
            var reason = new Label { Text = offer.Action.Reason, Visible = offer.Action.Reason.Length > 0,
                ClipText = true, TooltipText = offer.Action.Reason };
            reason.AddThemeFontSizeOverride("font_size", 10); actions.AddChild(reason);
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
        var feedbackHeight = _message.Visible ? 26 : 0;
        _message.Position = new Vector2(8, Size.Y - feedbackHeight);
        _message.Size = new Vector2(Mathf.Max(1, Size.X - 16), feedbackHeight);
        _scroll.Position = new Vector2(22, 16); _scroll.Size = new Vector2(Mathf.Max(1, Size.X - 44), Mathf.Max(1, Size.Y - 32 - feedbackHeight));
        _offers.AddThemeConstantOverride("separation", Mathf.RoundToInt(Mathf.Clamp(GetViewportRect().Size.X * .03f, 16, 40)));
        _offers.CustomMinimumSize = new Vector2(0, _scroll.Size.Y);
        foreach (var item in _items)
        {
            item.Layout();
        }
    }

    private void LayoutOffer(Control row, CardItemView card, int slots, VBoxContainer info, VBoxContainer actions)
    {
        var height = Mathf.Clamp(GetViewportRect().Size.Y * .16f, 116, 165);
        var infoWidth = GetViewportRect().Size.X <= 1150 ? 50 : 69;
        var cardGap = GetViewportRect().Size.X <= 1150 ? 8 : 12;
        row.CustomMinimumSize = new Vector2(height * slots / 2 + cardGap + infoWidth, _scroll.Size.Y);
        card.Position = new Vector2(0, (_scroll.Size.Y - height) / 2);
        card.Size = new Vector2(height * slots / 2, height);
        info.Position = new Vector2(card.Size.X + cardGap, card.Position.Y + height - 87);
        info.Size = new Vector2(infoWidth, 49); info.Show();
        if (info.GetNodeOrNull<Control>("MergePreview") is { } merge) { merge.Show(); info.Position -= new Vector2(0, 21); }
        actions.Position = new Vector2(info.Position.X, card.Position.Y + height - 35);
        actions.Size = new Vector2(infoWidth - 4, 32);
    }
}
