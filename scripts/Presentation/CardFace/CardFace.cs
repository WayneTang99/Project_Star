using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.CardFace;

// 卡面装饰组件（表现层），接受只读内容与可选实际显示尺寸。
public sealed partial class CardFace : Control
{
    private const float BaseHeight = 400;
    private const float WidthPerSize = 200;

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
    private PackedScene _effectScene = null!;
    private CardFaceViewModel? _viewModel;
    private bool _ready;
    private Vector2? _displaySize;

    public override void _Ready()
    {
        _frame = GetNode<Panel>("Frame");
        _innerFrame = GetNode<Panel>("InnerFrame");
        _sockets = new Control { Name = "GemSockets", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        AddChild(_sockets);
        _artFrame = GetNode<Panel>("ArtFrame");
        _artwork = GetNode<TextureRect>("ArtFrame/Artwork");
        _artInnerRim = GetNode<Panel>("ArtFrame/InnerRim");
        MoveChild(_artFrame, 1);
        _effects = GetNode<Control>("Effects"); _effects.ClipContents = false;
        _valueBadge = GetNode<CardValueBadge>("ValueBadge");
        _effects.ZIndex = _valueBadge.ZIndex = 1;
        _ornament = GetNode<NinePatchRect>("Ornament");
        _valuePlate = GetNode<Polygon2D>("ValuePlate");
        _valueRim = GetNode<Line2D>("ValueRim");
        _elementLayer = GetNode<Control>("ElementLayer");

        _elementScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardElementBadge.tscn");
        _effectScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardEffectRow.tscn");
        _ornament.Texture = GD.Load<Texture2D>("res://art/ui/card-face/card-frame.svg");
        _ornament.Hide(); _valueRim.Hide();
        var shade = new TextureRect { Name = "ArtShade", Texture = GD.Load<Texture2D>("res://art/ui/html/art-shade.svg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        _artFrame.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
        _valueBadge.SetValue(viewModel.CurrentValue);

        var levelColor = CardLevelGem.LevelColor(viewModel.Level);
        var frame = MakePanelStyle(new Color("19251f"), levelColor, 2, 6);
        frame.ShadowColor = new Color(0, 0, 0, .55f); frame.ShadowSize = 3; frame.ShadowOffset = new Vector2(0, 2);
        _frame.AddThemeStyleboxOverride("panel", frame);
        _innerFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(Colors.Transparent, new Color(levelColor, .65f), 1, 3));
        _artFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(Colors.Transparent, Colors.Transparent, 0, 0));
        _artInnerRim.Hide();
        _valuePlate.Color = AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Value)!.Value.Darkened(.65f);
        _valueRim.DefaultColor = new Color("#e9c77f");
        _ornament.Modulate = Colors.White;

        LayoutParts(Size.X);
        RebuildElements(viewModel);
        RebuildEffects(viewModel);
    }

    private void LayoutParts(float width)
    {
        var height = Size.Y;
        const float padding = 5;
        var compact = GetViewportRect().Size.X <= 1150;
        var font = compact ? 13 : 15;
        _artFrame.Position = Vector2.One * padding; _artwork.Position = Vector2.Zero;
        _artFrame.Size = _artwork.Size = Size - Vector2.One * padding * 2;
        _artInnerRim.Position = Vector2.One * padding;
        _artInnerRim.Size = Size - Vector2.One * padding * 2;
        _frame.Position = Vector2.Zero; _frame.Size = Size;
        _innerFrame.Position = Vector2.One * 4; _innerFrame.Size = Size - Vector2.One * 8;

        var scale = height / BaseHeight;
        _ornament.Position = Vector2.Zero; _ornament.Size = new Vector2(width / scale, BaseHeight);
        _ornament.Scale = Vector2.One * scale;
        // 宝石孔沿底边居中排列；等级仅通过整张卡框颜色表示。
        var sockets = _sockets.GetChildCount();
        var smallViewport = GetViewportRect().Size.X <= 650;
        var socketDiameter = smallViewport ? 6f : 8f;
        var socketGap = smallViewport ? 5f : 7f;
        var socketWidth = sockets * socketDiameter + Math.Max(0, sockets - 1) * socketGap;
        _sockets.Position = new Vector2((width - socketWidth) / 2, height - 4);
        _sockets.Size = new Vector2(socketWidth, socketDiameter);
        for (var index = 0; index < sockets; index++)
        {
            var socket = _sockets.GetChild<Panel>(index);
            socket.Position = new Vector2(index * (socketDiameter + socketGap), 0);
            socket.Size = Vector2.One * socketDiameter;
            socket.PivotOffset = socket.Size / 2;
            socket.RotationDegrees = 45;
        }
        _elementLayer.Size = Size;
        var count = _elementLayer.GetChildCount();
        const float diameter = 23;
        for (var index = 0; index < count; index++)
        {
            var badge = _elementLayer.GetChild<Control>(index);
            badge.Size = Vector2.One * diameter; badge.Scale = Vector2.One;
            badge.Position = new Vector2(width - 7 - count * diameter - (count - 1) * 4 + index * (diameter + 4), 7);
        }

        const float rowHeight = 25;
        var valueFont = compact ? 11 : 13;
        var valueWidth = Mathf.Max(35, MatchTheme.Font(true, true).GetStringSize(_viewModel!.CurrentValue.ToString(), fontSize: valueFont).X + 30);
        var valueLeft = width + 1 - valueWidth;
        var bottom = height - 7;
        var top = bottom - 23;
        _valuePlate.Polygon = [new(valueLeft, bottom), new(valueLeft + 9, top), new(width + 1, top), new(width + 1, bottom)];
        _valueRim.Points = [_valuePlate.Polygon[0], _valuePlate.Polygon[1], _valuePlate.Polygon[2]];
        _valueRim.Width = Mathf.Clamp(height * .008f, 1, 3);
        _valueBadge.CustomMinimumSize = Vector2.Zero;
        _valueBadge.SetDisplayMetrics(valueFont);
        _valueBadge.Position = new Vector2(valueLeft + 11, top + 3);
        _valueBadge.Size = new Vector2(valueWidth - 17, 17);
        var rows = _effects.GetChildren();
        var visibleEffects = Math.Min(rows.Count, Math.Max(1, (int)(height * .6f / (rowHeight + 3))));
        var effectWidth = Mathf.Max(40, width - 35);
        _effects.Position = new Vector2(-1, bottom - visibleEffects * rowHeight - Math.Max(0, visibleEffects - 1) * 3);
        _effects.Size = new Vector2(effectWidth, visibleEffects * (rowHeight + 3));
        for (var index = 0; index < rows.Count; index++)
        {
            var row = (CardEffectRow)rows[index];
            row.Visible = index < visibleEffects;
            row.SetDisplayMetrics(font, rowHeight);
            row.Position = new Vector2(5, index * (rowHeight + 3));
            row.Size = new Vector2(Mathf.Min(row.PreferredWidth(font), effectWidth) - 12, rowHeight);
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
            badge.CustomMinimumSize = Vector2.Zero;
            _elementLayer.AddChild(badge);
            badge.SetElement(elementKey);
        }
        LayoutParts(Size.X);
    }

    // 底边菱形凹槽表示空孔，镶嵌后使用宝石渐变；详细名称保留在卡牌详情。
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
            var style = MatchTheme.Plate(name is null ? "gem-socket-empty" : "gem-socket-filled", 0);
            style.ExpandMarginLeft = style.ExpandMarginRight = style.ExpandMarginTop = style.ExpandMarginBottom = 2;
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

}
