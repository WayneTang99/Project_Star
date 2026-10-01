using System;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

// 卡面装饰组件（表现层），接受只读内容与可选实际显示尺寸。
public sealed partial class CardFace : Control
{
    private const float BaseHeight = 400;
    private const float WidthPerSize = 200;
    private const float ElementBadgeDiameter = 58;
    private const float ElementBadgeGap = 4;

    private Panel _frame = null!;
    private Panel _innerFrame = null!;
    private Panel _header = null!;
    private Label _title = null!;
    private Panel _artFrame = null!;
    private Panel _artInnerRim = null!;
    private TextureRect _artwork = null!;
    private Panel _footer = null!;
    private VBoxContainer _effects = null!;
    private CardValueBadge _valueBadge = null!;
    private NinePatchRect _ornament = null!;
    private Polygon2D _bottomCrest = null!;
    private Polygon2D _bottomCrestLight = null!;
    private Control _elementLayer = null!;
    private PackedScene _elementScene = null!;
    private PackedScene _levelScene = null!;
    private PackedScene _effectScene = null!;
    private CardLevelGem _levelGem = null!;
    private CardFaceViewModel? _viewModel;
    private bool _ready;
    private Vector2? _displaySize;

    public override void _Ready()
    {
        _frame = GetNode<Panel>("Frame");
        _innerFrame = GetNode<Panel>("InnerFrame");
        _header = GetNode<Panel>("Header");
        _title = GetNode<Label>("Title");
        _title.ClipText = true;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _artFrame = GetNode<Panel>("ArtFrame");
        _artwork = GetNode<TextureRect>("ArtFrame/Artwork");
        _artInnerRim = GetNode<Panel>("ArtFrame/InnerRim");
        MoveChild(_artFrame, 1);
        _footer = GetNode<Panel>("Footer");
        _effects = GetNode<VBoxContainer>("Effects");
        _valueBadge = GetNode<CardValueBadge>("ValueBadge");
        _title.ZIndex = _effects.ZIndex = _valueBadge.ZIndex = 1;
        _ornament = GetNode<NinePatchRect>("Ornament");
        _bottomCrest = GetNode<Polygon2D>("BottomCrest");
        _bottomCrestLight = GetNode<Polygon2D>("BottomCrestLight");
        _elementLayer = GetNode<Control>("ElementLayer");

        _elementScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardElementBadge.tscn");
        _levelScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardLevelGem.tscn");
        _effectScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardEffectRow.tscn");
        _ornament.Texture = GD.Load<Texture2D>("res://art/ui/card-face/card-frame.svg");
        _levelGem = (CardLevelGem)_levelScene.Instantiate();
        AddChild(_levelGem);

        _artwork.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _artFrame.ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _ready = true;
        if (_viewModel is not null) Render(_viewModel);
    }

    // 更新卡面内容，布局不读取可变对局。
    public void SetCard(CardFaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        if (_ready) Render(viewModel);
    }

    // 棋盘按实际尺寸布局子模块；原始展示未指定尺寸时仍使用原生卡面。
    public void SetDisplaySize(Vector2 size)
    {
        if (size.X < 1 || size.Y < 1) return;
        _displaySize = size;
        CustomMinimumSize = Vector2.Zero; Size = size;
        if (_ready && _viewModel is not null) LayoutParts(size.X);
    }

    private void Render(CardFaceViewModel viewModel)
    {
        var width = WidthPerSize * (int)viewModel.Size;
        CustomMinimumSize = _displaySize is null ? new Vector2(width, BaseHeight) : Vector2.Zero;
        Size = _displaySize ?? CustomMinimumSize;
        _title.Text = viewModel.DisplayName;
        _artwork.Texture = viewModel.Artwork;
        _levelGem.SetLevel(viewModel.Level);
        _valueBadge.SetValue(viewModel.CurrentValue);

        var accent = FactionAccent(viewModel.FactionKey);
        _frame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#31251a"), new Color("#26180e"), 4, 12));
        _innerFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), accent.Lightened(0.1f), 1, 9));
        _header.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(.96f, .91f, .82f, .65f), Colors.Transparent, 0, 8));
        _artFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(Colors.Transparent, Colors.Transparent, 0, 0));
        _artInnerRim.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), new Color("#f3d99e"), 1, 7));
        _footer.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(.95f, .89f, .78f, .65f), Colors.Transparent, 0, 8));
        _ornament.Modulate = Colors.White;

        LayoutParts(Size.X);
        RebuildElements(viewModel);
        RebuildEffects(viewModel);
    }

    private void LayoutParts(float width)
    {
        // 插画始终覆盖整张卡牌；标题、底栏与徽章只在它上方叠加。
        _artFrame.Position = _artwork.Position = Vector2.Zero;
        _artFrame.Size = _artwork.Size = Size;
        _artInnerRim.Position = new Vector2(6, 6);
        _artInnerRim.Size = Size - new Vector2(12, 12);
        if (_displaySize is not null) { LayoutCompact(width, Size.Y); return; }
        const float height = BaseHeight;
        _frame.Position = Vector2.Zero;
        _frame.Size = new Vector2(width, height);
        _innerFrame.Position = new Vector2(5, 5);
        _innerFrame.Size = new Vector2(width - 10, height - 10);
        _header.Position = new Vector2(12, 18);
        _header.Size = new Vector2(width - 24, 52);
        _title.Position = new Vector2(16, 34);
        _title.Size = new Vector2(width - 32, 34);
        _ornament.Position = Vector2.Zero;
        _ornament.Size = new Vector2(width, height);
        _bottomCrest.Position = new Vector2(width * 0.5f, height - 5);
        _bottomCrestLight.Position = _bottomCrest.Position;
        _elementLayer.Position = Vector2.Zero;
        _elementLayer.Size = new Vector2(width, height);
        _levelGem.Position = new Vector2((width - 38) * 0.5f, -1);
        _levelGem.Size = new Vector2(38, 40);

        var effectHeight = 28f;
        var visibleRows = Math.Min(_viewModel!.Effects.Count, 5);
        var footerHeight = Math.Max(61, visibleRows * effectHeight + 36);
        _footer.Position = new Vector2(10, height - footerHeight - 9);
        _footer.Size = new Vector2(width - 20, footerHeight);
        _effects.Position = new Vector2(12, height - 27 - visibleRows * effectHeight);
        var effectWidth = Mathf.Max(
            48,
            Mathf.Min(96, width * 0.5f - ElementBadgeDiameter * 0.5f - 18));
        _effects.Size = new Vector2(effectWidth, visibleRows * effectHeight);
        _effects.AddThemeConstantOverride("separation", 0);
        foreach (var child in _effects.GetChildren()) ((CardEffectRow)child).SetDisplayMetrics(17, effectHeight);

        var valueWidth = width < 240 ? 62 : 76;
        _valueBadge.Position = new Vector2(width - valueWidth - 24, height - 43);
        _valueBadge.Size = new Vector2(valueWidth, 30);
        _valueBadge.CustomMinimumSize = new Vector2(valueWidth, 30);

        var elementCount = _elementLayer.GetChildCount();
        var groupWidth = elementCount == 0
            ? 0
            : elementCount * ElementBadgeDiameter + (elementCount - 1) * ElementBadgeGap;
        for (var index = 0; index < elementCount; index++)
        {
            if (_elementLayer.GetChild(index) is Control badge)
            {
                badge.Size = new Vector2(ElementBadgeDiameter, ElementBadgeDiameter);
                badge.Position = new Vector2(
                    width * 0.5f - groupWidth * 0.5f + index * (ElementBadgeDiameter + ElementBadgeGap),
                    height - footerHeight - 9 - ElementBadgeDiameter * 0.5f);
            }
        }
    }

    private void LayoutCompact(float width, float height)
    {
        var padding = Mathf.Clamp(height * .025f, 3, 10);
        var font = Mathf.Clamp(Mathf.RoundToInt(height * .06f), 13, 24);
        var rowHeight = Mathf.Clamp(height * .06f, 18, 24);
        var gemHeight = Mathf.Clamp(height * .1f, 18, 40);
        var headerBottom = gemHeight * .55f + font + 12;
        var rowCount = Math.Min(_viewModel!.Effects.Count, Math.Min(5, Math.Max(0, (int)((height - headerBottom - 42) / rowHeight))));
        var footerHeight = rowCount * rowHeight + 27;
        _frame.Position = Vector2.Zero; _frame.Size = Size;
        _innerFrame.Position = Vector2.One * padding; _innerFrame.Size = Size - Vector2.One * padding * 2;
        _header.Position = new Vector2(padding, padding); _header.Size = new Vector2(width - padding * 2, headerBottom - padding);
        _title.AddThemeFontSizeOverride("font_size", font);
        _title.Position = new Vector2(padding + 2, gemHeight * .55f + 3);
        _title.Size = new Vector2(Mathf.Max(1, width - padding * 2 - 4), font + 6);
        _footer.Position = new Vector2(padding, height - footerHeight);
        _footer.Size = new Vector2(width - padding * 2, footerHeight - padding);
        var ornamentScale = height / BaseHeight;
        _ornament.Position = Vector2.Zero;
        _ornament.Size = new Vector2(width / ornamentScale, BaseHeight);
        _ornament.Scale = Vector2.One * ornamentScale;
        _bottomCrest.Position = new Vector2(width / 2, height - 3); _bottomCrest.Scale = Vector2.One * height / BaseHeight;
        _bottomCrestLight.Position = _bottomCrest.Position; _bottomCrestLight.Scale = _bottomCrest.Scale;
        _levelGem.Size = new Vector2(38, 40); _levelGem.Scale = Vector2.One * gemHeight / 40;
        _levelGem.Position = new Vector2((width - 38 * _levelGem.Scale.X) / 2, 0);
        _elementLayer.Size = Size;
        var diameter = Mathf.Clamp(height * .09f, 18, 44);
        var count = _elementLayer.GetChildCount();
        for (var index = 0; index < count; index++)
        {
            var badge = _elementLayer.GetChild<Control>(index);
            badge.Size = Vector2.One * ElementBadgeDiameter; badge.Scale = Vector2.One * diameter / ElementBadgeDiameter;
            badge.Position = new Vector2(width / 2 - (count * diameter + (count - 1) * 2) / 2 + index * (diameter + 2),
                height - footerHeight - diameter);
        }
        _effects.Position = new Vector2(padding + 2, height - footerHeight);
        _effects.Size = new Vector2(Mathf.Max(1, width - padding * 2 - 4), rowCount * rowHeight);
        var rows = _effects.GetChildren();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = (CardEffectRow)rows[index];
            row.Visible = index >= rows.Count - rowCount; row.SetDisplayMetrics(Math.Min(font, 16), rowHeight);
        }
        var valueWidth = Mathf.Clamp(width * .5f, 36, 80);
        _valueBadge.CustomMinimumSize = Vector2.Zero;
        _valueBadge.SetDisplayMetrics(Math.Min(font, 16));
        _valueBadge.Position = new Vector2(width - valueWidth - padding, height - 25);
        _valueBadge.Size = new Vector2(valueWidth, 22);
    }

    private void RebuildElements(CardFaceViewModel viewModel)
    {
        foreach (var child in _elementLayer.GetChildren())
        {
            _elementLayer.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var elementKey in viewModel.ElementKeys)
        {
            var badge = (CardElementBadge)_elementScene.Instantiate();
            _elementLayer.AddChild(badge);
            badge.SetElement(elementKey);
        }
        LayoutParts(Size.X);
    }

    private void RebuildEffects(CardFaceViewModel viewModel)
    {
        foreach (var child in _effects.GetChildren())
        {
            _effects.RemoveChild(child);
            child.QueueFree();
        }
        var rowCount = Math.Min(viewModel.Effects.Count, 5);
        for (var index = rowCount - 1; index >= 0; index--)
        {
            var row = (CardEffectRow)_effectScene.Instantiate();
            _effects.AddChild(row);
            row.Configure(viewModel.Effects[index]);
        }
        LayoutParts(Size.X);
    }

    private static StyleBoxFlat MakePanelStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = new StyleBoxFlat { BgColor = background };
        style.SetBorderWidthAll(borderWidth);
        style.BorderColor = border;
        style.SetCornerRadiusAll(radius);
        return style;
    }

    private static Color FactionAccent(StringName factionKey) => factionKey == GameFactions.Neutral
        ? new Color("#b7b5b0")
        : factionKey == new StringName("paladin")
            ? new Color("#d2ad67")
            : new Color("#87a5bf");
}
