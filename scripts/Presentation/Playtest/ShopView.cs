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
    private ScrollContainer _scroll = null!;
    private VBoxContainer _offers = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private readonly List<CardItemView> _cards = new();
    private readonly CardDisplayAdapter _adapter = new();
    public override void _Ready()
    {
        _message = new RichTextLabel { Name = "Message" }; AddChild(_message);
        _scroll = new ScrollContainer { Name = "OfferScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _offers = new VBoxContainer { Name = "Offers", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_offers);
        Resized += LayoutView; LayoutView();
    }

    // 不生成商品，不计算价格，不消费随机数。
    public void Render(string message, IReadOnlyList<ShopItemViewModel> offers)
    {
        Clear(); _message.Text = message;
        foreach (var offer in offers)
        {
            var row = new HBoxContainer { Name = $"Offer{offer.Index}", Visible = offer.Action.Visible };
            _offers.AddChild(row);
            var card = new CardItemView { Name = "Card", CustomMinimumSize = new Vector2(70 * (int)offer.Card.Size, 140) };
            row.AddChild(card); card.Render(offer.Card, _adapter); card.DetailsRequested += ShowDetails; _cards.Add(card);
            var actions = new VBoxContainer { Name = "Actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(actions);
            actions.AddChild(new Label { Text = $"{offer.Card.DisplayName} · {offer.Card.Level}级\n购买价 {offer.Price} · 持有价值 {offer.Card.Value}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart });
            var button = new Button { Name = "Buy", ClipText = true,
                Text = offer.Action.Text, Disabled = !offer.Action.Enabled,
                TooltipText = CardDisplayAdapter.Details(offer.Card) + "\n" + offer.Action.Reason };
            Action handler = () => { if (!button.Disabled && button.Visible) BuyRequested?.Invoke(offer.Index, offer.Revision); };
            actions.AddChild(button); button.Pressed += handler; _bindings.Add((button, handler));
            actions.AddChild(new Label { Text = offer.Action.Reason, AutowrapMode = TextServer.AutowrapMode.WordSmart });
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
        _message.Size = new Vector2(Size.X, 50);
        _scroll.Position = new Vector2(0, 54); _scroll.Size = new Vector2(Size.X, Mathf.Max(1, Size.Y - 54));
        foreach (var card in _cards)
        {
            var slots = card.CustomMinimumSize.X / card.CustomMinimumSize.Y * 2;
            var height = Mathf.Max(80, Size.Y - 58);
            card.CustomMinimumSize = new Vector2(height * slots / 2, height);
        }
    }
}
