using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 上方出售目标仅在己方卡牌拖拽期间显示，通过意图调用应用出售用例。
public sealed partial class SellDropZone : PanelContainer
{
    public Func<BoardDragData, Result<int>>? PreviewRequested { get; set; }
    public event Action<BoardDragData, CardSnapshot>? DropRequested;
    private MatchSnapshot? _player;
    private bool _enabled;
    private Label _hint = null!;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color(.95f, .9f, .72f, .95f), MatchTheme.Gold));
        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_hint);
    }

    public void Render(MatchSnapshot? player, bool enabled)
    {
        _player = player;
        _enabled = enabled;
        if (!_enabled || _player is null) Hide();
    }

    private CardSnapshot? FindCard(Variant data)
    {
        if (!_enabled || _player is null || data.VariantType != Variant.Type.Object
            || data.AsGodotObject() is not BoardDragData drag || drag.MatchId != _player.MatchId
            || !_player.BoardPlacements.Any(placement => placement.CardId == drag.CardId)) return null;
        return _player.Cards.FirstOrDefault(card => card.Id == drag.CardId);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var card = FindCard(data);
        if (card is null || PreviewRequested is null) return false;
        var result = PreviewRequested((BoardDragData)data.AsGodotObject());
        var valid = result.IsSuccess && result.Value == card.Value;
        _hint.Text = valid ? $"松开出售 {card.DisplayName}\n金钱 +{card.Value} · 出售效果同时结算"
            : result.Failure?.Message ?? "卡牌信息已变化，请重新拖拽。";
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) return;
        var card = FindCard(data)!;
        Hide();
        DropRequested?.Invoke((BoardDragData)data.AsGodotObject(), card);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && _hint is not null)
        {
            var data = GetViewport().GuiGetDragData();
            if (FindCard(data) is null) return;
            _hint.Text = "拖到这里出售 · 松开确认";
            Show();
        }
        if (what == NotificationDragEnd) Hide();
    }
}
