using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Presentation.CardFace;
using Project_Star.Application.Match;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.Playtest;

// 战场与备战共用只读渲染器；窗口变化只计算布局，不读取或修改对局。
public sealed partial class BoardZoneView : Control
{
    public event Action<int>? SlotPressed;
    public event Action<CardSnapshot>? DetailsRequested;
    public event Action<BoardDragData, BoardZone, int>? DropRequested;
    public Func<BoardDragData, BoardZone, int, Result<BoardPlacementResult>>? PreviewRequested { get; set; }
    public int SharedCapacity { get; set; }
    private readonly Dictionary<EntityId, CardItemView> _cards = new();
    private readonly List<Button> _slots = new();
    private readonly CardDisplayAdapter _adapter = new();
    private IReadOnlyList<BoardPlacementSnapshot> _placements = Array.Empty<BoardPlacementSnapshot>();
    private Label _title = null!;
    private Control _preview = null!;
    private Guid _matchId;
    private BoardZone _zone;
    private bool _interactive;
    private float _unit;
    private float _left;
    private string _caption = "";
    private readonly Dictionary<EntityId, int> _levels = new();

    public override void _Ready()
    {
        _title = new Label();
        _title.ClipText = true;
        AddChild(_title);
        _preview = new Control { Name = "PlacementPreview", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 2, ClipContents = true };
        AddChild(_preview);
        Resized += LayoutBoard;
        MouseExited += ClearPreview;
    }

    // 权限由页面快照传入；敌方保留详情输入但不发送移动操作。
    public void Render(MatchSnapshot? snapshot, BoardZone zone, bool interactive, EntityId? selected, string title)
    {
        ClearPreview();
        var previousMatch = _matchId;
        var previousPlacements = _placements;
        _zone = zone; _interactive = interactive; _matchId = snapshot?.MatchId ?? Guid.Empty;
        Visible = snapshot is not null;
        _placements = snapshot?.BoardPlacements.Where(item => item.Zone == zone).ToArray()
            ?? Array.Empty<BoardPlacementSnapshot>();
        var capacity = snapshot is null ? 0 : zone == BoardZone.Battlefield ? snapshot.BattlefieldCapacity : snapshot.BenchCapacity;
        var used = _placements.Sum(item => item.EndExclusive - item.Start);
        _caption = $"{title}　{used}/{capacity}格 · {_placements.Count}张　右键详情 / Escape取消选择";
        _title.Text = _caption;
        while (_slots.Count > capacity)
        {
            var last = _slots[^1]; _slots.RemoveAt(_slots.Count - 1);
            RemoveChild(last); last.QueueFree();
        }
        while (_slots.Count < capacity)
        {
            var slot = _slots.Count;
            var button = new Button { Name = $"Slot{slot}", Text = $"{slot + 1}\n·" };
            button.Pressed += () => SlotPressed?.Invoke(slot);
            button.SetDragForwarding(default,
                Callable.From<Vector2, Variant, bool>((point, data) => _CanDropData(button.Position + point, data)),
                Callable.From<Vector2, Variant>((point, data) => _DropData(button.Position + point, data)));
            AddChild(button); _slots.Add(button);
        }
        foreach (var slot in _slots) slot.Disabled = !interactive;
        var ids = _placements.Select(item => item.CardId).ToHashSet();
        foreach (var id in _cards.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            var card = _cards[id]; _cards.Remove(id); RemoveChild(card); card.QueueFree();
        }
        foreach (var placement in _placements)
        {
            var acquired = !_cards.ContainsKey(placement.CardId);
            if (!_cards.TryGetValue(placement.CardId, out var item))
            {
                item = new CardItemView { Name = "Card_" + placement.CardId.Value.ToString("N") };
                var id = placement.CardId;
                item.Pressed += () =>
                {
                    var current = _placements.FirstOrDefault(p => p.CardId == id);
                    if (current is not null && !item.Disabled && !item.ConsumeClickSuppression())
                        SlotPressed?.Invoke(current.Start + item.ClickSlotOffset);
                };
                item.DetailsRequested += card => DetailsRequested?.Invoke(card);
                AddChild(item); _cards.Add(id, item);
            }
            item.Disabled = !interactive;
            item.SetSelected(placement.CardId == selected);
            item.Render(snapshot!.Cards.First(card => card.Id == placement.CardId), _adapter, _matchId);
            var current = snapshot.Cards.First(card => card.Id == placement.CardId);
            var previous = previousPlacements.FirstOrDefault(card => card.CardId == placement.CardId);
            var changed = acquired || previous?.Start != placement.Start
                || _levels.TryGetValue(placement.CardId, out var level) && level != current.Level;
            _levels[placement.CardId] = current.Level;
            if (!interactive || previousMatch != _matchId) item.ClearFeedback();
            else if (changed) item.Pulse();
        }
        foreach (var id in _levels.Keys.Where(id => !ids.Contains(id)).ToArray()) _levels.Remove(id);
        LayoutBoard();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        ClearPreview();
        if (!_interactive || data.VariantType != Variant.Type.Object || data.AsGodotObject() is not BoardDragData drag
            || drag.MatchId != _matchId || PreviewRequested is null || _unit <= 0
            || atPosition.Y < 28 || atPosition.Y >= 28 + _unit * 2) return false;
        var start = Mathf.FloorToInt((atPosition.X - _left) / _unit) - drag.GrabOffset;
        var result = PreviewRequested(drag, _zone, start);
        var color = result.IsSuccess ? new Color(0.3f, 0.9f, 0.5f, .35f) : new Color(1, .25f, .25f, .4f);
        AddPreview(start, drag.OccupiedSlots, color);
        _title.Text = result.IsSuccess ? $"可放置，推挤 {result.Value!.AffectedCards} 张卡牌。" : result.Failure!.Message;
        if (result.IsSuccess)
            foreach (var move in result.Value!.Moves)
                if (move.CardId != drag.CardId)
                {
                    var placement = _placements.First(item => item.CardId == move.CardId);
                    AddPreview(move.ToStart, placement.EndExclusive - placement.Start, new Color(.3f, .6f, 1, .35f));
                }
        return result.IsSuccess;
    }

    // 播放中的卡牌状态通过身份匹配到复用节点；其他页面清除临时装饰。
    public void RenderBattle(BattlePlaybackViewModel? playback)
    {
        foreach (var (id, card) in _cards)
        {
            var state = playback?.State.Cards.FirstOrDefault(item => item.Id == id);
            var activated = playback is not null && playback.Activations.TryGetValue(id, out var tick)
                && playback.Tick - tick <= 2;
            card.RenderBattle(state, activated);
        }
    }

    // 换页时停止界面反馈，不修改卡牌数据或几何。
    public void ClearFeedback() { foreach (var card in _cards.Values) card.ClearFeedback(); }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) return;
        var drag = (BoardDragData)data.AsGodotObject();
        var start = Mathf.FloorToInt((atPosition.X - _left) / _unit) - drag.GrabOffset;
        ClearPreview(); DropRequested?.Invoke(drag, _zone, start);
    }

    public override void _Notification(int what)
    { if (what == NotificationDragEnd && _preview is not null) ClearPreview(); }

    private void AddPreview(int start, int size, Color color) => _preview.AddChild(new ColorRect
    {
        Position = new Vector2(_left + start * _unit, 28), Size = new Vector2(size * _unit, _unit * 2),
        Color = color, MouseFilter = MouseFilterEnum.Ignore,
    });

    private void ClearPreview()
    {
        foreach (var child in _preview.GetChildren()) { _preview.RemoveChild(child); child.QueueFree(); }
        _title.Text = _caption;
    }

    public override void _ExitTree() { Resized -= LayoutBoard; MouseExited -= ClearPreview; }

    private void LayoutBoard()
    {
        ClearPreview(); _title.Size = new Vector2(Size.X, 24); _preview.Size = Size;
        if (_slots.Count == 0) return;
        var unit = Math.Max(1, Math.Min(Size.X / Math.Max(_slots.Count, SharedCapacity), (Size.Y - 28) / 2));
        var left = (Size.X - unit * Math.Max(_slots.Count, SharedCapacity)) / 2;
        _unit = unit; _left = left;
        for (var index = 0; index < _slots.Count; index++)
        {
            _slots[index].Position = new Vector2(left + index * unit, 28);
            _slots[index].Size = new Vector2(unit, unit * 2);
        }
        foreach (var placement in _placements)
        {
            var card = _cards[placement.CardId];
            card.Position = new Vector2(left + placement.Start * unit, 28);
            card.Size = new Vector2((placement.EndExclusive - placement.Start) * unit, unit * 2);
        }
    }
}
