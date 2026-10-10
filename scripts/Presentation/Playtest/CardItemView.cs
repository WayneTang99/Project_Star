using System;
using Godot;
using Project_Star.Presentation.CardFace;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using CardFaceControl = Project_Star.Presentation.CardFace.CardFace;

namespace Project_Star.Presentation.Playtest;

// 卡牌输入外层；卡面只负责装饰，详情与操作由外层分发。
public sealed partial class CardItemView : Button
{
    public event Action<CardSnapshot, Rect2>? DetailsRequested;
    private CardFaceControl _face = null!;
    private CardSnapshot? _card;
    private Guid _matchId;
    private Vector2 _pressPoint;
    private bool _suppressClick;
    private Panel _outline = null!;
    private bool _selected;
    private bool _hovered;
    private CardBattleOverlay _battleOverlay = null!;
    private Panel _feedback = null!;
    private Tween? _feedbackTween;
    private Label _selectedMark = null!;
    private CardBattleSnapshot? _battleState;
    private CardFaceViewModel? _faceModel;

    public override void _Ready()
    {
        _face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFaceControl>();
        AddChild(_face);
        _battleOverlay = new CardBattleOverlay { Name = "BattleOverlay", Visible = false, ZIndex = 2 };
        AddChild(_battleOverlay);
        _feedback = new Panel { Name = "ChangeFeedback", MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 3 };
        _feedback.AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color(1, .9f, .55f, .3f), MatchTheme.Gold));
        AddChild(_feedback); _feedback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _outline = new Panel { Name = "SelectionOutline", MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 3 };
        AddChild(_outline); _outline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _selectedMark = new Label { Text = "✓", MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 4 };
        _selectedMark.AddThemeFontSizeOverride("font_size", 13);
        _selectedMark.AddThemeColorOverride("font_color", MatchTheme.Ink); AddChild(_selectedMark);
        FocusEntered += UpdateOutline; FocusExited += UpdateOutline;
        MouseEntered += EnterHover; MouseExited += ExitHover;
        Resized += LayoutFace;
    }

    // 使用当前捕获的卡牌更新装饰，保留卡牌节点身份。
    public void Render(CardSnapshot card, CardDisplayAdapter adapter, Guid matchId = default)
    {
        _card = card;
        _matchId = matchId;
        _faceModel = adapter.Build(card);
        _face.SetCard(_faceModel);
        IgnoreDecoration(_face);
        TooltipText = "";
        LayoutFace();
    }

    // 临时状态和发动亮度只属于表现；摧毁不删除战前身份或修改模型。
    public void RenderBattle(CardBattleSnapshot? state, bool activated, BattlePlaybackViewModel? playback = null)
    {
        _battleState = state;
        _face.Modulate = state?.Destroyed == true ? new Color(.35f, .35f, .35f)
            : state?.IsBerserk == true ? new Color(1.08f, .95f, .9f)
            : state?.IsFlying == true ? new Color(1.03f, 1.08f, 1.05f) : Colors.White;
        _battleOverlay.Render(state, activated, playback);
        LayoutFace();
    }

    // 卡牌详情统一由页面展示，禁止引擎延时再生成第二个悬停窗。
    public override string _GetTooltip(Vector2 atPosition) => "";

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
        { _pressPoint = mouse.Position; _suppressClick = false; }
        if (input is InputEventKey { Pressed: true, Keycode: Key.Space or Key.Enter })
        { _pressPoint = Vector2.Zero; _suppressClick = false; }
        if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0
            && motion.Position.DistanceTo(_pressPoint) >= 8 && !GetViewport().GuiIsDragging())
        {
            var payload = CreateDragData();
            if (payload is not null) { ForceDrag(payload, DragPreview()); AcceptEvent(); }
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && _card is not null)
        {
            DetailsRequested?.Invoke(_card, GetGlobalRect());
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var payload = CreateDragData();
        if (payload is null) return default;
        SetDragPreview(DragPreview()); return payload;
    }

    private BoardDragData? CreateDragData()
    {
        if (Disabled || _card is null || _matchId == Guid.Empty) return null;
        _suppressClick = true;
        return new BoardDragData(_matchId, _card.Id, ClickSlotOffset, (int)_card.Size);
    }
    private Control DragPreview()
    {
        var preview = new Control { Name = "CardDragPreview", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 100, ZAsRelative = false,
            Scale = GetGlobalTransform().Scale };
        var face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFaceControl>();
        face.Name = "Face"; preview.AddChild(face);
        var size = _face.Size;
        var offset = _face.Position - _pressPoint;
        var model = _faceModel!;
        preview.Ready += () =>
        {
            face.SetCard(model); face.SetDisplaySize(size); face.Position = offset;
            IgnoreDecoration(preview);
        };
        FindShell()?.HideHoverCard();
        return preview;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && _card is not null
            && GetViewport().GuiGetDragData().AsGodotObject() is BoardDragData drag && drag.CardId == _card.Id)
            Modulate = new Color(1, 1, 1, .28f);
        if (what == NotificationDragEnd) Modulate = Colors.White;
    }

    // 点击和拖拽共用按下位置的格偏移，键盘操作默认起始格。
    public int ClickSlotOffset => _card is null ? 0 : Mathf.Clamp(
        Mathf.FloorToInt(_pressPoint.X / (GetParent() is BoardZoneView board ? board.SlotPitch : Mathf.Max(1, Size.X) / (int)_card.Size)), 0, (int)_card.Size - 1);

    public override bool _CanDropData(Vector2 atPosition, Variant data) => GetParent() is BoardZoneView board
        && board._CanDropData(Position + atPosition, data);

    public override void _DropData(Vector2 atPosition, Variant data)
    { if (GetParent() is BoardZoneView board) board._DropData(Position + atPosition, data); }

    // 拖拽释放不再触发点击移动。
    public bool ConsumeClickSuppression()
    { var suppressed = _suppressClick; _suppressClick = false; return suppressed; }

    // 独立轮廓保留选中与键盘焦点，不改变卡面颜色。
    public void SetSelected(bool selected)
    {
        if (selected && !_selected) Pulse();
        if (!selected && _selected) ClearFeedback();
        _selected = selected; _selectedMark.Visible = selected; UpdateOutline();
    }

    // 只给变化卡牌短暂强调；新反馈、取消与退出终止旧动画。
    public void Pulse()
    {
        ClearFeedback(); _feedback.Show(); _feedback.Modulate = Colors.White;
        _feedbackTween = CreateTween();
        _feedbackTween.TweenProperty(_feedback, "modulate:a", 0f, .22);
        _feedbackTween.TweenCallback(Callable.From(() => _feedback.Hide()));
    }

    // 清除临时强调不会改变卡牌位置、选中或对局数据。
    public void ClearFeedback() { _feedbackTween?.Kill(); _feedbackTween = null; _feedback.Hide(); }

    private void UpdateOutline()
    {
        _outline.Visible = _selected || HasFocus() || _hovered;
        var color = _selected ? MatchTheme.Gold : HasFocus() ? MatchTheme.Blue
            : CardLevelGem.LevelColor(_card?.Level ?? 1);
        var style = MatchTheme.Outline(color);
        if (!_selected && !HasFocus()) style.SetBorderWidthAll(1);
        var outset = _selected || HasFocus() ? 3 : 1;
        style.ExpandMarginLeft = style.ExpandMarginRight = style.ExpandMarginTop = style.ExpandMarginBottom = outset;
        _outline.AddThemeStyleboxOverride("panel", style);
    }
    private Tween? _hoverTween;
    private float _hoverOffset;
    private void EnterHover()
    {
        _hovered = true; UpdateOutline(); _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.TweenMethod(Callable.From<float>(offset => { _hoverOffset = offset; LayoutFace(); }), _hoverOffset, -6f, .2);
        if (_card is not null) FindShell()?.ShowHoverCard(_card, GetGlobalRect(), _battleState);
    }
    private void ExitHover()
    {
        _hovered = false; UpdateOutline(); _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.TweenMethod(Callable.From<float>(offset => { _hoverOffset = offset; LayoutFace(); }), _hoverOffset, 0f, .2);
        FindShell()?.HideHoverCard();
    }
    private MatchShell? FindShell()
    {
        for (var parent = GetParent(); parent is not null; parent = parent.GetParent()) if (parent is MatchShell shell) return shell;
        return null;
    }

    public override void _ExitTree()
    { _hoverTween?.Kill(); ClearFeedback(); Resized -= LayoutFace; FocusEntered -= UpdateOutline; FocusExited -= UpdateOutline;
        MouseEntered -= EnterHover; MouseExited -= ExitHover; }

    private void LayoutFace()
    {
        if (_card is null) return;
        var ratio = (int)_card.Size / 2f;
        var height = Mathf.Min(Size.Y, Size.X / ratio);
        var faceSize = GetParent() is BoardZoneView ? Size : new Vector2(height * ratio, height);
        _face.SetDisplaySize(faceSize);
        _face.Position = (Size.IsEqualApprox(faceSize) ? Vector2.Zero : (Size - faceSize) / 2) + new Vector2(0, _hoverOffset);
        _battleOverlay.Position = _face.Position; _battleOverlay.Size = faceSize;
        _selectedMark.Position = new Vector2(4, Mathf.Max(0, Size.Y - 25));
    }

    private static void IgnoreDecoration(Node node)
    {
        if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) IgnoreDecoration(child);
    }
}
