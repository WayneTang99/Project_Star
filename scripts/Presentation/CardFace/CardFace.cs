using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

// 卡面装饰组件（表现层），接受只读内容与可选实际显示尺寸。
public sealed partial class CardFace : Control
{
    private const float BaseHeight = 400;
    private const float WidthPerSize = 200;
    private const float ElementBadgeDiameter = 58;

    private Panel _frame = null!;
    private Panel _innerFrame = null!;
    private Control _sockets = null!;
    private Panel _artFrame = null!;
    private Panel _artInnerRim = null!;
    private TextureRect _artwork = null!;
    private Control _effects = null!;
    private CardValueBadge _valueBadge = null!;
    private NinePatchRect _ornament = null!;
    private Polygon2D _valuePlate = null!;
    private Line2D _valueRim = null!;
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
        _sockets = new Control { Name = "GemSockets", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 1 };
        AddChild(_sockets);
        _artFrame = GetNode<Panel>("ArtFrame");
        _artwork = GetNode<TextureRect>("ArtFrame/Artwork");
        _artInnerRim = GetNode<Panel>("ArtFrame/InnerRim");
        MoveChild(_artFrame, 1);
        _effects = GetNode<Control>("Effects"); _effects.ClipContents = true;
        _valueBadge = GetNode<CardValueBadge>("ValueBadge");
        _effects.ZIndex = _valueBadge.ZIndex = 1;
        _ornament = GetNode<NinePatchRect>("Ornament");
        _valuePlate = GetNode<Polygon2D>("ValuePlate");
        _valueRim = GetNode<Line2D>("ValueRim");
        _elementLayer = GetNode<Control>("ElementLayer");

        _elementScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardElementBadge.tscn");
        _levelScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardLevelGem.tscn");
        _effectScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardEffectRow.tscn");
        _ornament.Texture = GD.Load<Texture2D>("res://art/ui/card-face/card-frame.svg");
        _levelGem = (CardLevelGem)_levelScene.Instantiate();
        _levelGem.Name = "LevelGem";
        _levelGem.ZIndex = 2;
        AddChild(_levelGem);
        _elementLayer.ZIndex = 2;

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
        RebuildSockets(viewModel);
        _artwork.Texture = viewModel.Artwork;
        _levelGem.SetLevel(viewModel.Level);
        _valueBadge.SetValue(viewModel.CurrentValue);

        var accent = FactionAccent(viewModel.FactionKey);
        _frame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#31251a"), new Color("#26180e"), 4, 12));
        _innerFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), accent.Lightened(0.1f), 1, 9));
        _artFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(Colors.Transparent, Colors.Transparent, 0, 0));
        _artInnerRim.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), new Color("#f3d99e"), 1, 7));
        _valuePlate.Color = new Color("#795329");
        _valueRim.DefaultColor = new Color("#e9c77f");
        _ornament.Modulate = Colors.White;

        LayoutParts(Size.X);
        RebuildElements(viewModel);
        RebuildEffects(viewModel);
    }

    private void LayoutParts(float width)
    {
        var height = Size.Y;
        var padding = Mathf.Clamp(height * .018f, 2, 7);
        var font = Mathf.Clamp(Mathf.RoundToInt(height * .07f), 14, 20);
        _artFrame.Position = _artwork.Position = Vector2.Zero;
        _artFrame.Size = _artwork.Size = Size;
        _artInnerRim.Position = Vector2.One * padding;
        _artInnerRim.Size = Size - Vector2.One * padding * 2;
        _frame.Position = Vector2.Zero; _frame.Size = Size;
        _innerFrame.Position = Vector2.One * padding; _innerFrame.Size = Size - Vector2.One * padding * 2;

        var scale = height / BaseHeight;
        _ornament.Position = Vector2.Zero; _ornament.Size = new Vector2(width / scale, BaseHeight);
        _ornament.Scale = Vector2.One * scale;
        // 等级与元素占据上方两角，中央留给插画；宝石孔紧随等级下方。
        var gemHeight = Mathf.Min(Mathf.Clamp(height * .12f, 20, 40), width * .32f);
        _levelGem.Size = new Vector2(38, 40); _levelGem.Scale = Vector2.One * gemHeight / 40;
        _levelGem.Position = new Vector2(padding - gemHeight * .12f, -gemHeight * .08f);

        _sockets.Position = new Vector2(padding, _levelGem.Position.Y + gemHeight + 3);
        _sockets.Size = new Vector2(Mathf.Max(1, width * .45f - padding), gemHeight);
        var sockets = _sockets.GetChildCount();
        var socketDiameter = Mathf.Min(height < BaseHeight ? 9 : 16, _sockets.Size.X / Math.Max(1, sockets) * .8f);
        for (var index = 0; index < sockets; index++)
        {
            var socket = _sockets.GetChild<Panel>(index);
            socket.Position = new Vector2(index * socketDiameter * 1.25f, 0);
            socket.Size = Vector2.One * socketDiameter;
        }
        _elementLayer.Size = Size;
        var count = _elementLayer.GetChildCount();
        var diameter = Mathf.Min(Mathf.Clamp(height * .11f, 18, 38), (width * .48f - Math.Max(0, count - 1) * 2) / Math.Max(1, count));
        for (var index = 0; index < count; index++)
        {
            var badge = _elementLayer.GetChild<Control>(index);
            badge.Size = Vector2.One * ElementBadgeDiameter; badge.Scale = Vector2.One * diameter / ElementBadgeDiameter;
            badge.Position = new Vector2(width - padding - count * diameter - (count - 1) * 2 + index * (diameter + 2), padding);
        }

        var rowHeight = Mathf.Clamp(height * .11f, 24, 36);
        var valueWidth = Mathf.Min(width * .42f,
            font * .65f * _viewModel!.CurrentValue.ToString().Length + font * .65f + padding * 2);
        var slant = Mathf.Min(rowHeight * .4f, width * .07f);
        var valueLeft = width - padding - valueWidth;
        var bottom = height - padding;
        var top = bottom - rowHeight;
        _valuePlate.Polygon = [new(valueLeft - slant, bottom), new(valueLeft, top), new(width - padding, top), new(width - padding, bottom)];
        _valueRim.Points = [_valuePlate.Polygon[0], _valuePlate.Polygon[1], _valuePlate.Polygon[2]];
        _valueRim.Width = Mathf.Clamp(height * .008f, 1, 3);
        _valueBadge.CustomMinimumSize = Vector2.Zero;
        _valueBadge.SetDisplayMetrics(font);
        _valueBadge.Position = new Vector2(valueLeft + padding, top + 2);
        _valueBadge.Size = new Vector2(Mathf.Max(1, valueWidth - padding * 2), rowHeight - 4);
        var rows = _effects.GetChildren();
        var visibleEffects = Math.Min(rows.Count, Math.Max(1, (int)(height * .45f / rowHeight)));
        var effectWidth = Mathf.Max(1, Mathf.Min(font * .65f * (_viewModel.Effects.Count == 0 ? 1
            : _viewModel.Effects.Max(effect => effect.Value.Length)) + font + 5, valueLeft - slant - padding - 2));
        _effects.Position = new Vector2(padding, bottom - visibleEffects * rowHeight);
        _effects.Size = new Vector2(effectWidth, visibleEffects * rowHeight);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = (CardEffectRow)rows[index];
            row.Visible = index < visibleEffects;
            row.SetDisplayMetrics(font, rowHeight);
            row.Position = new Vector2(0, index * rowHeight);
            row.Size = new Vector2(effectWidth, rowHeight);
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

    // 圆形凹槽表示空孔，镶嵌后以明亮宝石填充；详细名称保留在卡牌详情。
    private void RebuildSockets(CardFaceViewModel viewModel)
    {
        foreach (var child in _sockets.GetChildren())
        {
            _sockets.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var name in viewModel.GemNames)
        {
            var socket = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            var style = MakePanelStyle(name is null ? new Color("#17212b") : new Color("#52cfe7"),
                new Color("#e9c77f"), 2, 20);
            style.ShadowColor = new Color(0, 0, 0, .6f);
            style.ShadowSize = 2;
            socket.AddThemeStyleboxOverride("panel", style);
            _sockets.AddChild(socket);
        }
        _sockets.Visible = viewModel.GemNames.Count > 0;
    }

    private void RebuildEffects(CardFaceViewModel viewModel)
    {
        foreach (var child in _effects.GetChildren())
        {
            _effects.RemoveChild(child);
            child.QueueFree();
        }
        var rowCount = Math.Min(viewModel.Effects.Count, 5);
        for (var index = 0; index < rowCount; index++)
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
