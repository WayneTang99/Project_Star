using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 商店展示只读报价，购买意图始终带上产生按钮时的报价版本。
public sealed partial class ShopView : Control
{
    public event Action<int, long>? BuyRequested;
    public event Action<Project_Star.Application.Match.CardSnapshot>? DetailsRequested;
    private RichTextLabel _message = null!;
    private CardLevelGem _levelGem = null!;
    private Label _levelLabel = null!;
    private ScrollContainer _scroll = null!;
    private HBoxContainer _offers = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private readonly List<CardItemView> _cards = new();
    private readonly CardDisplayAdapter _adapter = new();
    public override void _Ready()
    {
        _message = new RichTextLabel { Name = "Message" }; AddChild(_message);
        _levelGem = new CardLevelGem { Name = "ShopLevelCrystal", MouseFilter = MouseFilterEnum.Ignore }; AddChild(_levelGem);
        _levelLabel = new Label { Name = "ShopLevel", MouseFilter = MouseFilterEnum.Ignore }; AddChild(_levelLabel);
        _scroll = new ScrollContainer { Name = "OfferScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _offers = new HBoxContainer { Name = "Offers", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_offers);
        Resized += LayoutView; LayoutView();
    }

    // 不生成商品，不计算价格，不消费随机数。
    public void Render(string message, IReadOnlyList<ShopItemViewModel> offers, int shopLevel = 0)
    {
        Clear(); _message.Text = message;
        _levelGem.Visible = _levelLabel.Visible = shopLevel > 0;
        _levelGem.SetLevel(shopLevel);
        _levelLabel.Text = $"{shopLevel}级商店";
        _levelLabel.TooltipText = $"商品等级不高于 {shopLevel} 级";
        foreach (var offer in offers)
        {
            var row = new VBoxContainer { Name = $"Offer{offer.Index}", Visible = offer.Action.Visible,
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _offers.AddChild(row);
            var card = new CardItemView { Name = "Card", CustomMinimumSize = new Vector2(70 * (int)offer.Card.Size, 140),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            row.AddChild(card); card.Render(offer.Card, _adapter); card.DetailsRequested += ShowDetails; _cards.Add(card);
            var identity = new HBoxContainer { Name = "Identity", Alignment = BoxContainer.AlignmentMode.Center }; row.AddChild(identity);
            var name = new Label { Text = offer.Card.DisplayName, ClipText = true,
                CustomMinimumSize = new Vector2(80, 0), TooltipText = CardDisplayAdapter.Details(offer.Card) };
            name.AddThemeFontSizeOverride("font_size", 14); identity.AddChild(name);
            var level = new CardLevelGem { Name = "Level", CustomMinimumSize = new Vector2(30, 40),
                MouseFilter = MouseFilterEnum.Ignore, TooltipText = $"等级 {offer.Card.Level}" };
            identity.AddChild(level); level.SetLevel(offer.Card.Level);
            foreach (var element in offer.Card.ElementKeys)
            {
                var badge = new CardElementBadge { CustomMinimumSize = new Vector2(40, 40),
                    MouseFilter = MouseFilterEnum.Ignore };
                identity.AddChild(badge); badge.SetElement(element);
            }
            var actions = new VBoxContainer { Name = "Actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(actions);
            var button = new Button { Name = "Buy", ClipText = true,
                Text = offer.Action.Text == "已售罄" ? "已售罄" : $"{offer.Action.Text} · {offer.Price} 金币", Disabled = !offer.Action.Enabled,
                TooltipText = CardDisplayAdapter.Details(offer.Card) + $"\n购买价 {offer.Price} · 持有价值 {offer.Card.Value}\n" + offer.Action.Reason };
            Action handler = () => { if (!button.Disabled && button.Visible) BuyRequested?.Invoke(offer.Index, offer.Revision); };
            actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
            actions.AddChild(new Label { Text = offer.Action.Reason, Visible = offer.Action.Reason.Length > 0,
                ClipText = true, TooltipText = offer.Action.Reason });
        }
        LayoutView();
    }

    public override void _ExitTree() { Resized -= LayoutView; Clear(); }
    private void Clear()
    {
        foreach (var (button, handler) in _bindings)
            button.Pressed -= handler;
        foreach (var card in _cards) card.DetailsRequested -= ShowDetails;
        foreach (var child in _offers.GetChildren()) { _offers.RemoveChild(child); child.QueueFree(); }
        _cards.Clear();
        _bindings.Clear();
    }
    private void ShowDetails(Project_Star.Application.Match.CardSnapshot card) => DetailsRequested?.Invoke(card);
    private void LayoutView()
    {
        var inset = _levelGem.Visible ? 112 : 0;
        _levelGem.Position = Vector2.Zero; _levelGem.Size = new Vector2(30, 38);
        _levelLabel.Position = new Vector2(36, 0); _levelLabel.Size = new Vector2(76, 38);
        _message.Position = new Vector2(inset, 0); _message.Size = new Vector2(Mathf.Max(1, Size.X - inset), 38);
        _scroll.Position = new Vector2(0, 42); _scroll.Size = new Vector2(Size.X, Mathf.Max(1, Size.Y - 42));
        foreach (var card in _cards)
        {
            var slots = card.CustomMinimumSize.X / card.CustomMinimumSize.Y * 2;
            var columnWidth = (Size.X - Mathf.Max(0, _cards.Count - 1) * MatchTheme.Gap) / Mathf.Max(1, _cards.Count);
            var height = Mathf.Max(40, Mathf.Min(Size.Y - 150, columnWidth / 1.5f));
            card.CustomMinimumSize = new Vector2(height * slots / 2, height);
        }
    }
}
