using System;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

/// <summary>Composable card face: content is supplied as a read-only presentation model.</summary>
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

    public override void _Ready()
    {
        _frame = GetNode<Panel>("Frame");
        _innerFrame = GetNode<Panel>("InnerFrame");
        _header = GetNode<Panel>("Header");
        _title = GetNode<Label>("Title");
        _artFrame = GetNode<Panel>("ArtFrame");
        _artwork = GetNode<TextureRect>("ArtFrame/Artwork");
        _artInnerRim = GetNode<Panel>("ArtFrame/InnerRim");
        _footer = GetNode<Panel>("Footer");
        _effects = GetNode<VBoxContainer>("Effects");
        _valueBadge = GetNode<CardValueBadge>("ValueBadge");
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

    public void SetCard(CardFaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        if (_ready) Render(viewModel);
    }

    private void Render(CardFaceViewModel viewModel)
    {
        var width = WidthPerSize * (int)viewModel.Size;
        CustomMinimumSize = new Vector2(width, BaseHeight);
        Size = CustomMinimumSize;
        _title.Text = viewModel.DisplayName;
        _artwork.Texture = viewModel.Artwork;
        _levelGem.SetLevel(viewModel.Level);
        _valueBadge.SetValue(viewModel.CurrentValue);

        var accent = FactionAccent(viewModel.FactionKey);
        _frame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#31251a"), new Color("#26180e"), 4, 12));
        _innerFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), accent.Lightened(0.1f), 1, 9));
        _header.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#f5e9d0"), new Color(0, 0, 0, 0), 0, 8));
        _artFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#24180f"), accent.Darkened(0.25f), 3, 9));
        _artInnerRim.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), new Color("#f3d99e"), 1, 7));
        _footer.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#f1e3c8"), new Color(0, 0, 0, 0), 0, 8));
        _ornament.Modulate = Colors.White;

        LayoutParts(width);
        RebuildElements(viewModel);
        RebuildEffects(viewModel);
    }

    private void LayoutParts(float width)
    {
        const float height = BaseHeight;
        _frame.Position = Vector2.Zero;
        _frame.Size = new Vector2(width, height);
        _innerFrame.Position = new Vector2(5, 5);
        _innerFrame.Size = new Vector2(width - 10, height - 10);
        _header.Position = new Vector2(12, 18);
        _header.Size = new Vector2(width - 24, 52);
        _title.Position = new Vector2(16, 34);
        _title.Size = new Vector2(width - 32, 34);
        _artFrame.Position = new Vector2(10, 68);
        _artFrame.Size = new Vector2(width - 20, 266);
        _artwork.Position = new Vector2(3, 3);
        _artwork.Size = _artFrame.Size - new Vector2(6, 6);
        _artInnerRim.Position = new Vector2(6, 6);
        _artInnerRim.Size = _artFrame.Size - new Vector2(12, 12);
        _footer.Position = new Vector2(10, 330);
        _footer.Size = new Vector2(width - 20, 61);
        _ornament.Position = Vector2.Zero;
        _ornament.Size = new Vector2(width, height);
        _bottomCrest.Position = new Vector2(width * 0.5f, height - 5);
        _bottomCrestLight.Position = _bottomCrest.Position;
        _elementLayer.Position = Vector2.Zero;
        _elementLayer.Size = new Vector2(width, height);
        _levelGem.Position = new Vector2((width - 38) * 0.5f, -1);
        _levelGem.Size = new Vector2(38, 40);

        var effectHeight = 24f;
        var visibleRows = Math.Min(_viewModel!.Effects.Count, 5);
        _effects.Position = new Vector2(12, height - 9 - visibleRows * effectHeight);
        var effectWidth = Mathf.Max(
            48,
            Mathf.Min(96, width * 0.5f - ElementBadgeDiameter * 0.5f - 18));
        _effects.Size = new Vector2(effectWidth, visibleRows * effectHeight);
        _effects.AddThemeConstantOverride("separation", 0);

        var valueWidth = width < 240 ? 62 : 76;
        _valueBadge.Position = new Vector2(width - valueWidth - 12, height - 43);
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
                    331 - ElementBadgeDiameter * 0.5f);
            }
        }
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
