using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

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
        _price.Text = $"镶嵌 · {offer.Price}金币";
        _hint.Text = offer.Resolved ? "本次已完成镶嵌" : "将发动卡牌拖到此处";
        TooltipText = offer.Description; Layout();
    }
    private void Layout()
    {
        var width = Math.Max(1, Size.X - 144);
        _name.Position = new Vector2(132, 14); _name.Size = new Vector2(width, 28);
        _description.Position = new Vector2(132, 48); _description.Size = new Vector2(width, 68);
        _price.Position = new Vector2(132, 118); _price.Size = new Vector2(width, 24);
        _hint.Position = new Vector2(12, Math.Max(148, Size.Y - 30)); _hint.Size = new Vector2(Math.Max(1, Size.X - 24), 28);
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
        var color = _offer.Color; var center = new Vector2(65, 64);
        MatchTheme.DrawSurface(this, new Rect2(Vector2.Zero, Size), "stage");
        DrawRect(new Rect2(Vector2.One * 3, Size - Vector2.One * 6), new Color(color, _valid ? 1 : .3f), false, _valid ? 2 : 1);
        DrawCircle(center, 46, new Color(color, .08f));
        var stone = new Vector2[] { center + new Vector2(0, -30), center + new Vector2(24, -4), center + new Vector2(0, 29), center + new Vector2(-24, -4) };
        DrawColoredPolygon(stone, new Color(color, _offer.Resolved ? .35f : .9f));
        DrawLine(stone[0], stone[2], new Color("ffffff", .35f), 1); DrawLine(stone[1], stone[3], new Color("ffffff", .35f), 1);
        var top = center + new Vector2(0, 47);
        DrawColoredPolygon(new Vector2[] { top + new Vector2(-46, -5), top + new Vector2(46, -5), top + new Vector2(28, 9),
            top + new Vector2(12, 12), top + new Vector2(12, 26), top + new Vector2(31, 37), top + new Vector2(-31, 37),
            top + new Vector2(-12, 26), top + new Vector2(-12, 12), top + new Vector2(-28, 9) }, new Color("6d8076"));
        DrawLine(top + new Vector2(-40, -4), top + new Vector2(40, -4), new Color(color, .7f), 2);
    }
}
