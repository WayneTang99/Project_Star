using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 左下二级入口的只读浮层；奖励按钮携带对局与列表版本。
public sealed partial class HeroDetailsView : PanelContainer
{
    public event Action<Guid, int, long>? ClaimRequested;
    public event Action<CardSnapshot, Rect2>? DetailsRequested;
    private VBoxContainer _items = null!;
    private Button _close = null!;
    private Control? _returnFocus;
    private HeroSection _section;
    private MatchPageViewModel? _view;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private readonly List<CardItemView> _cards = new();
    private readonly CardDisplayAdapter _adapter = new();
    public override void _Ready()
    {
        var column = new VBoxContainer { Name = "Content" }; AddChild(column);
        _close = new Button { Text = "关闭", Name = "Close" }; column.AddChild(_close); _close.Pressed += Hide;
        var scroll = new ScrollContainer { Name = "Scroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; column.AddChild(scroll);
        _items = new VBoxContainer { Name = "Items", SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_items);
        VisibilityChanged += RestoreFocus; Hide();
    }

    // 刷新已打开的浮层，领取失败仍显示原有奖励和局部反馈。
    public void Render(MatchPageViewModel view)
    {
        _view = view;
        if (view.Player is null) Hide();
        if (Visible) Populate();
    }

    // 仅打开只读内容，不调用业务用例。
    public void ShowSection(HeroSection section, Vector2 bounds)
    {
        if (_view?.Player is null) return;
        if (!Visible) _returnFocus = GetViewport().GuiGetFocusOwner();
        _section = section; Show(); Populate();
        Size = new Vector2(Mathf.Min(520, bounds.X), Mathf.Min(450, bounds.Y));
        Position = (bounds - Size) / 2;
        _close.GrabFocus();
    }

    // 窗口变化只调整几何尺寸。
    public void ClampTo(Vector2 bounds)
    {
        Size = new Vector2(Mathf.Min(520, bounds.X), Mathf.Min(450, bounds.Y));
        Position = new Vector2(Mathf.Clamp(Position.X, 0, Mathf.Max(0, bounds.X - Size.X)),
            Mathf.Clamp(Position.Y, 0, Mathf.Max(0, bounds.Y - Size.Y)));
    }

    public override void _Input(InputEvent input)
    {
        var focus = GetViewport().GuiGetFocusOwner();
        if (IsVisibleInTree() && input is InputEventKey { Pressed: true, Keycode: Key.Tab } tab
            && (focus is null || IsAncestorOf(focus))) MatchMenuView.TrapTab(this, tab);
    }
    private void RestoreFocus()
    {
        if (Visible) return;
        var focus = GetViewport().GuiGetFocusOwner();
        if ((focus is null || IsAncestorOf(focus)) && GodotObject.IsInstanceValid(_returnFocus)
            && _returnFocus!.IsInsideTree() && _returnFocus.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }
    public override void _ExitTree() { VisibilityChanged -= RestoreFocus; _close.Pressed -= Hide; Clear(); }
    private void Populate()
    {
        Clear(); var view = _view!; var player = view.Player!;
        if (_section == HeroSection.Skills)
        {
            Text("技能");
            if (player.Skills.Count == 0) Text("尚未获得技能。");
            foreach (var skill in player.Skills)
            {
                var row = new HBoxContainer(); _items.AddChild(row);
                var icon = new SkillItemView { CustomMinimumSize = new Vector2(72, 72),
                    SizeFlagsVertical = SizeFlags.ShrinkBegin, FocusMode = FocusModeEnum.None };
                row.AddChild(icon); icon.Render(skill, _adapter);
                var description = new RichTextLabel { FitContent = true, ScrollActive = false,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill };
                row.AddChild(description); CardKeywordText.Render(description, CardDisplayAdapter.SkillDetails(skill));
            }
        }
        else if (_section == HeroSection.Sets)
        {
            Text("套装 · 仅统计战场不同卡牌");
            if (player.Sets.Count == 0) Text("尚无套装。");
            foreach (var set in player.Sets)
            {
                Text($"{set.DisplayName} · 已有 {set.DistinctCardCount} 种");
                foreach (var threshold in set.Thresholds)
                    Text($"{threshold.RequiredCount}件 · {(threshold.Active ? "已生效" : "未激活")}\n"
                        + CardDisplayAdapter.AbilityDetails(threshold.Abilities));
            }
            Text("卡牌任务进度请在卡牌详情查看。");
        }
        else
        {
            Text("待领奖励"); Text(view.Message);
            if (view.Rewards.Count == 0) Text("没有待领奖励。");
            foreach (var reward in view.Rewards)
            {
                Text(reward.Text);
                CardItemView? rewardCard = null;
                if (reward.Card is { } snapshot)
                {
                    var card = new CardItemView { Name = $"RewardCard{reward.Index}", CustomMinimumSize = new Vector2(70 * (int)snapshot.Size, 140),
                        SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                    _items.AddChild(card); card.Render(snapshot, _adapter); card.DetailsRequested += ShowCard; _cards.Add(card);
                    rewardCard = card;
                }
                else Text(reward.Details);
                Control hint = rewardCard is null ? new Button { Text = "领取", Disabled = !view.Reward.Enabled }
                    : new Label { Text = "点击卡牌获取", MouseFilter = MouseFilterEnum.Ignore };
                hint.Name = $"Claim{reward.Index}";
                Action handler = () => { if (IsVisibleInTree() && view.Reward.Enabled && (rewardCard is null || !rewardCard.ConsumeClickSuppression()))
                    ClaimRequested?.Invoke(player.MatchId, reward.Index, reward.Revision); };
                _items.AddChild(hint);
                if (rewardCard is null) LevelPresentation.Frame(hint, reward.Level, 3);
                if (hint is Button skill) { skill.Pressed += handler; _bindings.Add((skill, handler)); }
                if (rewardCard is not null) { rewardCard.Pressed += handler; _bindings.Add((rewardCard, handler)); }
            }
        }
    }
    private void Text(string text) => _items.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });
    private void ShowCard(CardSnapshot card, Rect2 rect) => DetailsRequested?.Invoke(card, rect);
    private void Clear()
    {
        foreach (var (button, handler) in _bindings) button.Pressed -= handler;
        foreach (var card in _cards) card.DetailsRequested -= ShowCard;
        _bindings.Clear(); _cards.Clear();
        foreach (var child in _items.GetChildren()) { _items.RemoveChild(child); child.QueueFree(); }
    }
}
