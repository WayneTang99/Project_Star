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
    private Panel _header = null!;
    private Label _title = null!;
    private Control _sockets = null!;
    private Panel _artFrame = null!;
    private Panel _artInnerRim = null!;
    private TextureRect _artwork = null!;
    private Panel _footer = null!;
    private Control _effects = null!;
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
        _sockets = new Control { Name = "GemSockets", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 1 };
        AddChild(_sockets);
        _artFrame = GetNode<Panel>("ArtFrame");
        _artwork = GetNode<TextureRect>("ArtFrame/Artwork");
        _artInnerRim = GetNode<Panel>("ArtFrame/InnerRim");
        MoveChild(_artFrame, 1);
        _footer = GetNode<Panel>("Footer");
        _effects = GetNode<Control>("Effects"); _effects.ClipContents = true;
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
        RebuildSockets(viewModel);
        _artwork.Texture = viewModel.Artwork;
        _levelGem.SetLevel(viewModel.Level);
        _valueBadge.SetValue(viewModel.CurrentValue);

        var accent = FactionAccent(viewModel.FactionKey);
        _frame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color("#31251a"), new Color("#26180e"), 4, 12));
        _innerFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), accent.Lightened(0.1f), 1, 9));
        _header.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(.98f, .95f, .86f, .88f), Colors.Transparent, 0, 3));
        _artFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(Colors.Transparent, Colors.Transparent, 0, 0));
        _artInnerRim.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0), new Color("#f3d99e"), 1, 7));
        _footer.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(.98f, .95f, .86f, .88f), Colors.Transparent, 0, 3));
        _ornament.Modulate = Colors.White;

        LayoutParts(Size.X);
        RebuildElements(viewModel);
        RebuildEffects(viewModel);
    }

    private void LayoutParts(float width)
    {
        var height = Size.Y;
        var padding = Mathf.Clamp(height * .018f, 2, 7);
        // 所有尺寸共享等高上下栏；栏高与效果数量无关，插画始终覆盖整卡。
        var band = Mathf.Clamp(height * .14f, 22, 56);
        var font = Mathf.Clamp(Mathf.RoundToInt(height * .065f), 12, 24);
        _artFrame.Position = _artwork.Position = Vector2.Zero;
        _artFrame.Size = _artwork.Size = Size;
        _artInnerRim.Position = Vector2.One * padding;
        _artInnerRim.Size = Size - Vector2.One * padding * 2;
        _frame.Position = Vector2.Zero; _frame.Size = Size;
        _innerFrame.Position = Vector2.One * padding; _innerFrame.Size = Size - Vector2.One * padding * 2;
        _header.Position = Vector2.Zero; _header.Size = new Vector2(width, band);
        _footer.Position = new Vector2(0, height - band); _footer.Size = new Vector2(width, band);
        _title.AddThemeFontSizeOverride("font_size", font);
        _title.Position = new Vector2(padding + 2, 0);
        _title.Size = new Vector2(Mathf.Max(1, width - padding * 2 - 4), band);

        var scale = height / BaseHeight;
        _ornament.Position = Vector2.Zero; _ornament.Size = new Vector2(width / scale, BaseHeight);
        _ornament.Scale = Vector2.One * scale;
        _bottomCrest.Position = new Vector2(width / 2, height - 1); _bottomCrest.Scale = Vector2.One * scale;
        _bottomCrestLight.Position = _bottomCrest.Position; _bottomCrestLight.Scale = _bottomCrest.Scale;
        var gemHeight = Mathf.Clamp(height * .12f, 20, 40);
        _levelGem.Size = new Vector2(38, 40); _levelGem.Scale = Vector2.One * gemHeight / 40;
        _levelGem.Position = new Vector2(padding + 1, band + 2);

        _sockets.Position = new Vector2(padding + gemHeight + 3, band + 4);
        _sockets.Size = new Vector2(Mathf.Max(1, width - padding * 2 - gemHeight - 3), gemHeight);
        var sockets = _sockets.GetChildCount();
        var socketDiameter = Mathf.Min(height < BaseHeight ? 9 : 16, _sockets.Size.X / Math.Max(1, sockets) * .8f);
        for (var index = 0; index < sockets; index++)
        {
            var socket = _sockets.GetChild<Panel>(index);
            socket.Position = new Vector2(index * socketDiameter * 1.25f, 0);
            socket.Size = Vector2.One * socketDiameter;
        }
        _elementLayer.Size = Size;
        var diameter = Mathf.Clamp(height * .09f, 16, 44);
        var count = _elementLayer.GetChildCount();
        for (var index = 0; index < count; index++)
        {
            var badge = _elementLayer.GetChild<Control>(index);
            badge.Size = Vector2.One * ElementBadgeDiameter; badge.Scale = Vector2.One * diameter / ElementBadgeDiameter;
            badge.Position = new Vector2(width / 2 - (count * diameter + (count - 1) * 2) / 2 + index * (diameter + 2),
                height - band - diameter - 2);
        }

        var valueFont = height < 220 ? 11 : Mathf.Min(font, 18);
        var valueWidth = Mathf.Max(22, valueFont * .65f * _viewModel!.CurrentValue.ToString().Length + valueFont + 3);
        var rowHeight = Mathf.Max(18, band - 4);
        _valueBadge.CustomMinimumSize = Vector2.Zero;
        _valueBadge.SetDisplayMetrics(valueFont);
        _valueBadge.Position = new Vector2(width - valueWidth - padding, height - band + 2);
        _valueBadge.Size = new Vector2(valueWidth, rowHeight);
        _effects.Position = new Vector2(padding + 1, height - band + 2);
        _effects.Size = new Vector2(Mathf.Max(1, width - valueWidth - padding * 2 - 4), rowHeight);
        var rows = _effects.GetChildren();
        var visibleEffects = Math.Min(rows.Count, Math.Max(1, (int)(_effects.Size.X / (valueFont * 1.7f + 2))));
        var effectWidth = _effects.Size.X / Math.Max(1, visibleEffects);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = (CardEffectRow)rows[index];
            row.Visible = index < visibleEffects;
            row.SetDisplayMetrics(valueFont, rowHeight);
            row.Position = new Vector2(index * effectWidth, 0);
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
