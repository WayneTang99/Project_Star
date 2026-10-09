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
    public event Action<CardSnapshot>? DetailsRequested;
    private CardFaceControl _face = null!;
    private CardSnapshot? _card;
    private Guid _matchId;
    private Vector2 _pressPoint;
    private bool _suppressClick;
    private Panel _outline = null!;
    private bool _selected;
    private bool _hovered;
    private Label _battleStatus = null!;
    private ColorRect _cooldownMask = null!;
    private float _cooldownRemaining;
    private Panel _feedback = null!;
    private Tween? _feedbackTween;
    private Label _selectedMark = null!;
    private CardBattleSnapshot? _battleState;

    public override void _Ready()
    {
        _face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFaceControl>();
        AddChild(_face);
        _cooldownMask = new ColorRect { Name = "CooldownMask", Visible = false,
            MouseFilter = MouseFilterEnum.Ignore, Color = new Color("62c8e3"), ZIndex = 2 };
        AddChild(_cooldownMask);
        _battleStatus = new Label { Name = "BattleStatus", Visible = false, MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true, HorizontalAlignment = HorizontalAlignment.Center, ZIndex = 3 };
        _battleStatus.AddThemeFontSizeOverride("font_size", 16);
        _battleStatus.AddThemeColorOverride("font_color", Colors.White);
        _battleStatus.AddThemeConstantOverride("outline_size", 5);
        _battleStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_battleStatus);
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
        _face.SetCard(adapter.Build(card));
        IgnoreDecoration(_face);
        TooltipText = CardDisplayAdapter.Details(card) + (_selected ? "\n已选中" : "");
        LayoutFace();
    }

    // 临时状态和发动亮度只属于表现；摧毁不删除战前身份或修改模型。
    public void RenderBattle(CardBattleSnapshot? state, bool activated)
    {
        _battleState = state;
        _battleStatus.Visible = state is not null;
        _face.Modulate = state?.Destroyed == true ? new Color(.35f, .35f, .35f)
            : activated ? new Color(1.3f, 1.3f, .85f) : Colors.White;
        _cooldownRemaining = 0;
        if (state is { Destroyed: false, IsOnBench: false })
        {
            for (var index = 0; index < state.CooldownUnits.Count && index < state.CooldownDurationUnits.Count; index++)
            {
                var duration = state.CooldownDurationUnits[index];
                if (duration > 0)
                    _cooldownRemaining = Mathf.Max(_cooldownRemaining,
                        (float)Math.Clamp(state.CooldownUnits[index] / duration, 0m, 1m));
            }
        }
        _cooldownMask.Visible = _cooldownRemaining > 0;
        LayoutFace();
        if (state is null) return;
        var status = string.Join(" · ", Array.FindAll(new[] {
            state.IsFlying ? "飞行" : "", state.IsBerserk ? "狂暴" : "",
            state.Immobilize > 0 ? $"禁锢 {state.Immobilize / 10m:0.0}s" : "",
            state.Haste > 0 ? $"疾速 {state.Haste / 10m:0.0}s" : "",
            state.Slow > 0 ? $"迟缓 {state.Slow / 10m:0.0}s" : "" }, text => text.Length > 0));
        _battleStatus.Text = state.Destroyed ? "已摧毁" : status;
        _battleStatus.Visible = _battleStatus.Text.Length > 0;
        if (_card is not null) TooltipText = CardDisplayAdapter.Details(_card) + "\n" + status
            + (state.Destroyed ? "\n已摧毁" : "");
        LayoutFace();
    }

    // 卡牌悬停提示与详情共用关键词配色，TooltipText仍保持纯文本。
    public override GodotObject _MakeCustomTooltip(string forText)
    {
        if (_card is not null)
        {
            var content = new CardDetailsContent { Theme = MatchTheme.Create(), CustomMinimumSize = new Vector2(288, 0) };
            content.Ready += () => content.Render(_card, 288, Mathf.Max(60, GetViewportRect().Size.Y - 240));
            return content;
        }
        var text = new RichTextLabel { FitContent = true, ScrollActive = false,
            CustomMinimumSize = new Vector2(420, 0), MouseFilter = MouseFilterEnum.Ignore };
        text.Theme = MatchTheme.Create();
        CardKeywordText.RenderDetails(text, forText);
        return text;
    }

    public override string _GetTooltip(Vector2 atPosition) => FindShell() is null ? TooltipText : "";

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
            DetailsRequested?.Invoke(_card);
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
    private Label DragPreview() => new() { Text = _card!.DisplayName, MouseFilter = MouseFilterEnum.Ignore };

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
    private void EnterHover()
    {
        _hovered = true; UpdateOutline(); _hoverTween?.Kill();
        _hoverTween = CreateTween(); _hoverTween.TweenProperty(_face, "position:y", -6f, .2);
        if (_card is not null) FindShell()?.ShowHoverCard(_card, GetGlobalRect(), _battleState);
    }
    private void ExitHover()
    {
        _hovered = false; UpdateOutline(); _hoverTween?.Kill();
        _hoverTween = CreateTween(); _hoverTween.TweenProperty(_face, "position:y", 0f, .2);
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
        _face.Position = (Size.IsEqualApprox(faceSize) ? Vector2.Zero : (Size - faceSize) / 2) + new Vector2(0, _hovered ? -6 : 0);
        _cooldownMask.Position = _face.Position + new Vector2(0, faceSize.Y - Mathf.Clamp(faceSize.Y * .14f, 22, 56) - 3);
        _cooldownMask.Size = new Vector2(faceSize.X * (1 - _cooldownRemaining), 3);
        _battleStatus.Position = new Vector2(0, Mathf.Max(24, Size.Y * .35f));
        _battleStatus.Size = new Vector2(Size.X, 36);
        _selectedMark.Position = new Vector2(4, Mathf.Max(0, Size.Y - 25));
    }

    private static void IgnoreDecoration(Node node)
    {
        if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) IgnoreDecoration(child);
    }
}
