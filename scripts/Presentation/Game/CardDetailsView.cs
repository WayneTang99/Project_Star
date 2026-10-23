using System;
using System.Collections.Generic;
using Project_Star.Application.Content;
using Godot;
using Project_Star.Presentation.CardFace;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Game;

// 浮动详情不参与主界面布局，窗口缩放时限制在可见区域内。
public sealed partial class CardDetailsView : PanelContainer
{
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? SellRequested;
    public event Action? Closed;
    private CardRelatedEffectsView _related = null!;
    private float RelatedWidth => _related.Visible ? _related.Size.X + 16 : 0;
    private float _relatedX;
    private CardDetailsContent _content = null!;
    private ScrollContainer _detailScroll = null!;
    private int _restoreScroll;
    private int _restoreScrollFrames;
    private Button _sell = null!;
    private Label _interactionHint = null!;
    private CardSnapshot? _selling;
    private Guid _matchId;
    private Button _close = null!;
    private Vector2 _bounds;
    private CardSnapshot? _card;
    private CardBattleSnapshot? _battle;
    private Tween? _fade;
    private Vector2 _placementPosition;
    private float _revealOffset;
    private float _pixelScale = 1;
    private Rect2? _anchor;
    private int _placementVersion;
    private bool _pinned = true;
    public EntityId? CurrentCardId { get; private set; }
    public StringName? CurrentCardKey => _card?.Key;
    public override void _Ready()
    {
        _detailScroll = new ScrollContainer { Name = "Content", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_detailScroll);
        var plate = new StyleBoxEmpty { ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 22, ContentMarginBottom = 22 };
        AddThemeStyleboxOverride("panel", plate);
        _related = new CardRelatedEffectsView { Name = "Related", TopLevel = true, ZAsRelative = false, ZIndex = ZIndex }; AddChild(_related);
        Resized += QueueRedraw;
        _close = new Button { Name = "Close", Text = "×", Alignment = HorizontalAlignment.Center, TopLevel = true, ZAsRelative = false, ZIndex = ZIndex + 1, Size = new Vector2(21.36f, 26) }; AddChild(_close);
        var closePlate = new StyleBoxEmpty { ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4 };
        foreach (var state in new[] { "normal", "hover", "pressed" }) _close.AddThemeStyleboxOverride(state, closePlate);
        _close.AddThemeFontSizeOverride("font_size", 18);
        _close.AddThemeFontOverride("font", new FontVariation { BaseFont = MatchTheme.Font(), SpacingTop = -3, SpacingBottom = -4 });
        _close.AddThemeColorOverride("font_color", MatchTheme.Muted);
        _close.AddThemeColorOverride("font_hover_color", new Color("eedfc4"));
        _close.Size = _close.GetCombinedMinimumSize();
        _close.Pressed += Close;
        _content = new CardDetailsContent { Name = "CardContent", ExpandContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }; _detailScroll.AddChild(_content); Hide();
        _interactionHint = new Label { Name = "InteractionHint", Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(_interactionHint, 13, MatchTheme.Gold); _content.AddChild(_interactionHint);
        _sell = new Button { Name = "Sell", Visible = false, ClipText = true }; _content.AddChild(_sell);
        _sell.AddThemeFontSizeOverride("font_size", 16);
        _sell.Pressed += ConfirmSale;
    }

    // 关联卡牌只读取应用目录快照。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries) => _related.SetEntries(entries);

    // 商品与奖励的操作说明只属于当前视图，不写入内容定义。
    public void SetInteractionHint(string text) { _interactionHint.Text = text; _interactionHint.Visible = text.Length > 0; }

    // 展示完整卡牌语义，位置来自用户点击而非模型数据。
    public void ShowCard(CardSnapshot card, Vector2 position, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        SetInteractionHint("");
        _placementVersion++;
        _fade?.Kill(); _revealOffset = 0; _anchor = null; _relatedX = 0;
        CurrentCardId = card.Id;
        RenderContent(card, bounds, battle, false);
        Size = new Vector2(Size.X, 0);
        _selling = null; _sell.Hide(); _detailScroll.ScrollVertical = 0;
        _placementPosition = position + new Vector2(16 / _pixelScale, 0); Show();
        _revealOffset = 6; ClampTo(bounds);
        Modulate = new Color(1, 1, 1, 0); _fade = CreateTween();
        _fade.TweenMethod(Callable.From<float>(progress =>
        {
            var eased = CssEase(progress);
            Modulate = new Color(1, 1, 1, eased); _revealOffset = 6 * (1 - eased); ClampTo(_bounds);
        }), 0f, 1f, .12);
    }

    // 详情紧邻卡牌，右侧不足时翻到左侧，最后限制在窗口内。
    public void ShowNear(CardSnapshot card, Rect2 anchor, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        ShowCard(card, new Vector2(anchor.End.X, anchor.Position.Y), bounds, battle);
        _anchor = anchor;
        PlaceNearAnchor(anchor); ClampTo(_bounds);
        var version = _placementVersion;
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(this) || !Visible || version != _placementVersion) return;
            PlaceNearAnchor(anchor); ClampTo(_bounds);
        }).CallDeferred();
    }

    // 选中卡牌显示明确金额的出售确认；普通右键详情不提供出售。
    public void ShowSelected(CardSnapshot card, Guid matchId, UiAction sell, Vector2 position, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        SetPinned(true);
        if (Visible && CurrentCardId == card.Id) RefreshCard(card, bounds, battle);
        else ShowCard(card, position, bounds, battle);
        _selling = card; _matchId = matchId;
        _sell.Text = $"确认出售 · 获得 {card.Value} 金币";
        _sell.Visible = sell.Visible; _sell.Disabled = !sell.Enabled; _sell.TooltipText = _sell.Text + "\n" + sell.Reason;
    }

    // 回放刷新时复用原位置与卡牌身份，避免详情因每帧Render而消失。
    public void RefreshCard(CardSnapshot card, Vector2 bounds, CardBattleSnapshot? battle = null)
    {
        if (!Visible || CurrentCardId != card.Id) return;
        RenderContent(card, bounds, battle, true);
        ClampTo(bounds);
    }

    private void RenderContent(CardSnapshot card, Vector2 bounds, CardBattleSnapshot? battle, bool preserveExpansion)
    {
        if (!preserveExpansion) _restoreScrollFrames = 0;
        if (preserveExpansion && _restoreScrollFrames == 0 && _detailScroll.ScrollVertical > 0)
        {
            _restoreScroll = _detailScroll.ScrollVertical;
            _restoreScrollFrames = 3;
        }
        var resized = _bounds != bounds || !Mathf.IsEqualApprox(_pixelScale, GetViewport().GetStretchTransform().Scale.X);
        _bounds = bounds; _card = card; _battle = battle;
        _pixelScale = Mathf.Max(.01f, GetViewport().GetStretchTransform().Scale.X);
        Scale = Vector2.One / _pixelScale;
        LevelPresentation.Frame(this, card.Level);
        var pixels = bounds * _pixelScale;
        var width = Mathf.Max(1, Mathf.Min(pixels.X - 24, pixels.X <= 650 ? pixels.X - 24 : pixels.X < 900 ? 340 : 440));
        var relatedWidth = Mathf.Min(310, Mathf.Max(210, pixels.X - width - 56));
        _content.CustomMinimumSize = new Vector2(Mathf.Max(1, width - 44), 0);
        _content.Render(card, Mathf.Max(1, width - 44), Mathf.Max(60, pixels.Y - 280), battle, preserveExpansion);
        _related.ExpandContent = !_pinned;
        _related.Render(card, relatedWidth, Mathf.Max(100, pixels.Y - 40));
        SetPinned(_pinned);
        Size = new Vector2(width, GetCombinedMinimumSize().Y);
        if (resized && _anchor is { } anchor) PlaceNearAnchor(anchor);
    }

    public override void _Draw() => MatchTheme.DrawSurface(this, new Rect2(Vector2.Zero, Size), "tooltip");
    // 悬停详情只展示并穿透鼠标，固定详情保留操作入口。
    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        MouseFilter = pinned ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _detailScroll.VerticalScrollMode = pinned ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled;
        SetInput(GetNode<Control>("Content")); SetInput(_related);
        void SetInput(Control control)
        {
            control.MouseFilter = pinned && control is (Button or ScrollContainer or RichTextLabel)
                ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            foreach (var child in control.GetChildren()) if (child is Control next) SetInput(next);
        }
    }
    public override void _ExitTree() { _fade?.Kill(); Resized -= QueueRedraw; _sell.Pressed -= ConfirmSale; _close.Pressed -= Close; }
    private void Close() { _fade?.Kill(); _placementVersion++; Hide(); CurrentCardId = null; _card = null; Closed?.Invoke(); }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        var pixels = _bounds * _pixelScale;
        var maximum = Mathf.Max(120, pixels.Y - 68);
        _detailScroll.CustomMinimumSize = new Vector2(0, _pinned ? Mathf.Min(maximum, _content.GetCombinedMinimumSize().Y) : _content.GetCombinedMinimumSize().Y);
        if (_restoreScrollFrames > 0)
        {
            _detailScroll.ScrollVertical = _restoreScroll;
            _restoreScrollFrames--;
        }
        var height = GetCombinedMinimumSize().Y;
        if (!Mathf.IsEqualApprox(Size.Y, height)) Size = new Vector2(Size.X, height);
        ClampTo(_bounds);
        _related.Scale = GetGlobalTransform().Scale;
        _related.GlobalPosition = _anchor is not null
            ? new Vector2(((Control)GetParent()).GlobalPosition.X + _relatedX, GlobalPosition.Y)
            : GlobalPosition + new Vector2(Size.X + 16, 0) * _related.Scale;
        _related.Modulate = Modulate;
        _close.Scale = GetGlobalTransform().Scale;
        _close.GlobalPosition = GlobalPosition + new Vector2(Size.X - 9 - _close.Size.X, 7) * _close.Scale;
    }
    private void ConfirmSale()
    {
        if (_selling is { } card && !_sell.Disabled && Visible)
            SellRequested?.Invoke(_matchId, card.Id, card.Level, card.Value);
    }

    // 窗口变化时重新排版已有快照，保持详情和属性展开状态。
    public void ClampTo(Vector2 bounds)
    {
        var resized = _bounds != bounds || !Mathf.IsEqualApprox(_pixelScale, GetViewport().GetStretchTransform().Scale.X);
        if (resized && _card is not null)
        {
            RenderContent(_card, bounds, _battle, true);
        }
        _bounds = bounds;
        var margin = 12 / _pixelScale;
        if (Size.X * Scale.X > bounds.X - margin * 2) Size = new Vector2(Mathf.Max(1, bounds.X * _pixelScale - 24), Size.Y);
        if (_anchor is { } anchor) PlaceNearAnchor(anchor);
        var groupSize = GroupSize();
        var limit = bounds - groupSize * Scale - Vector2.One * margin;
        _placementPosition = new Vector2(Mathf.Clamp(_placementPosition.X, margin, Mathf.Max(margin, limit.X)),
            Mathf.Clamp(_placementPosition.Y, margin, Mathf.Max(margin, limit.Y)));
        Position = new Vector2(_placementPosition.X, Mathf.Min(_placementPosition.Y + _revealOffset / _pixelScale,
            Mathf.Max(margin, limit.Y)));
    }

    private void PlaceNearAnchor(Rect2 anchor)
    {
        var margin = 12 / _pixelScale; var gap = 16 / _pixelScale;
        var right = _bounds.X - margin - anchor.End.X - gap;
        var left = anchor.Position.X - gap - margin;
        var size = GroupSize() * Scale;
        var relatedWidth = RelatedWidth * Scale.X;
        float x;
        if (right >= size.X + relatedWidth) { x = anchor.End.X + gap; _relatedX = x + size.X + gap; }
        else if (left >= size.X + relatedWidth) { x = anchor.Position.X - size.X - gap; _relatedX = x - relatedWidth; }
        else if (right >= left) { x = anchor.End.X + gap; _relatedX = x + size.X + gap; }
        else { x = anchor.Position.X - size.X - gap; _relatedX = x - relatedWidth; }
        var y = Mathf.Clamp(anchor.Position.Y, margin, Mathf.Max(margin, _bounds.Y - margin - size.Y));
        _placementPosition = new Vector2(x, y);
    }

    private Vector2 GroupSize() => new(Size.X,
        Mathf.Max(Size.Y, _related.Visible ? _related.Size.Y : 0));

    // CSS默认ease为cubic-bezier(.25,.1,.25,1)，先按时间反解横轴再求进度。
    private static float CssEase(float progress)
    {
        if (progress <= 0 || progress >= 1) return progress;
        var low = 0f; var high = 1f;
        for (var index = 0; index < 16; index++)
        {
            var t = (low + high) / 2; var inverse = 1 - t;
            var x = .75f * inverse * inverse * t + .75f * inverse * t * t + t * t * t;
            if (x < progress) low = t; else high = t;
        }
        var value = (low + high) / 2; var remaining = 1 - value;
        return .3f * remaining * remaining * value + 3 * remaining * value * value + value * value * value;
    }
}
