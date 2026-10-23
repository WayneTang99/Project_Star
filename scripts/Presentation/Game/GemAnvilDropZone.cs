using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Game;

// 砧板是拖放目标；只发出意图，不写卡牌或金钱。
public sealed partial class GemAnvilDropZone : Control
{
    public Func<BoardDragData, int, long, Result>? PreviewRequested { get; set; }
    public event Action<BoardDragData, int, long>? DropRequested;
    private MatchSnapshot? _player;
    private GemAnvilViewModel? _offer;
    private Label _name = null!;
    private Label _description = null!;
    private Label _hint = null!;
    private Label _price = null!;
    private bool _valid;
    private Texture2D? _stone;
    internal string DropHint => _hint.Text;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _name = Label(this, 18, MatchTheme.Gold);
        _description = Label(this, 12, MatchTheme.Ink);
        _price = Label(this, 16, MatchTheme.Gold);
        _hint = Label(this, 12, MatchTheme.Muted);
        Resized += Layout;
    }
    private static Label Label(Control parent, int size, Color color)
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(label, size, color); parent.AddChild(label); return label;
    }
    public void Render(MatchSnapshot? player, GemAnvilViewModel offer)
    {
        _player = player; _offer = offer; _valid = false;
        _name.Text = offer.DisplayName; _name.AddThemeColorOverride("font_color", offer.Color);
        _description.Text = offer.Description;
        using var image = new Image();
        var light = offer.Color.Lightened(.45f).ToHtml(false); var shade = offer.Color.Darkened(.55f).ToHtml(false); var main = offer.Color.ToHtml(false);
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='50' height='60'><defs><linearGradient id='g'><stop stop-color='#{light}'/><stop offset='.48' stop-color='#{main}'/><stop offset='1' stop-color='#{shade}'/></linearGradient></defs><path d='M25 4L44 18L50 40L25 60L0 40L6 18Z' fill='#483621' opacity='.3'/><path d='M25 0L44 14L50 36L25 56L0 36L6 14Z' fill='url(#g)'/><path d='M25 2L39 16L34 37L25 52L16 37L11 16Z' fill='#fff' opacity='.2'/><path d='M6 14L16 37L0 36M44 14L34 37L50 36M16 37L25 56L34 37M6 14H44' fill='none' stroke='#fff' stroke-opacity='.25'/></svg>");
        _stone = ImageTexture.CreateFromImage(image);
        _price.Text = $"镶嵌 · {offer.Price}金币";
        _hint.Text = offer.Resolved ? "本次已完成镶嵌" : "将发动卡牌拖到此处";
        TooltipText = offer.Description; Layout();
    }
    private void Layout()
    {
        var width = Math.Max(1, Size.X);
        _name.Position = new Vector2(0, 80); _name.Size = new Vector2(width, 22);
        _name.AddThemeFontSizeOverride("font_size", 14);
        _description.Hide(); _hint.Hide();
        _price.Position = new Vector2(0, 108); _price.Size = new Vector2(width, 22);
        _price.AddThemeFontSizeOverride("font_size", 14);
        _price.AddThemeColorOverride("font_color", new Color("493b2d"));
        QueueRedraw();
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        _valid = false;
        if (_player is null || _offer is null || _offer.Resolved || data.VariantType != Variant.Type.Object
            || data.AsGodotObject() is not BoardDragData drag || drag.MatchId != _player.MatchId
            || !_player.BoardPlacements.Any(placement => placement.CardId == drag.CardId) || PreviewRequested is null) return false;
        var result = PreviewRequested(drag, _offer.Index, _offer.Revision);
        _valid = result.IsSuccess; _hint.Text = _valid ? "松开镶嵌" : result.Failure!.Message;
        QueueRedraw(); return _valid;
    }
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) return;
        DropRequested?.Invoke((BoardDragData)data.AsGodotObject(), _offer!.Index, _offer.Revision);
    }
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _offer is not null)
        { _valid = false; _hint.Text = _offer.Resolved ? "本次已完成镶嵌" : "将发动卡牌拖到此处"; QueueRedraw(); }
    }
    public override void _Draw()
    {
        if (_offer is null) return;
        var color = _offer.Color;
        if (_valid) DrawRect(new Rect2(Vector2.One * 3, Size - Vector2.One * 6), new Color(color, .9f), false, 2);
        if (_stone is not null) DrawTextureRect(_stone, new Rect2(Size.X / 2 - 25, 12, 50, 60), false,
            _offer.Resolved ? new Color(1, 1, 1, .35f) : Colors.White);
    }
}
