using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 结算展示已结算的消息与待领图卡，领取仍由应用入口执行。
public sealed partial class ResultView : Control
{
    public event Action<Guid, int, long>? ClaimRequested;
    public event Action<CardSnapshot>? DetailsRequested;
    private Label _summary = null!;
    private RichTextLabel _log = null!;
    private ScrollContainer _scroll = null!;
    private HBoxContainer _rewards = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private readonly List<CardItemView> _cards = new();
    private readonly CardDisplayAdapter _adapter = new();

    public override void _Ready()
    {
        var emblem = new TextureRect { Texture = MatchTheme.Icon("battle"), Position = new Vector2(12, 12),
            Size = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(emblem);
        _summary = new Label { Position = new Vector2(72, 10), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_summary);
        _scroll = new ScrollContainer { Name = "Rewards", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever }; AddChild(_scroll);
        _rewards = new HBoxContainer(); _scroll.AddChild(_rewards);
        _log = new RichTextLabel { Visible = false, ZIndex = 1 }; AddChild(_log);
        Resized += LayoutView; LayoutView();
    }

    // 按同批快照展示奖励，动态按钮释放旧连接，重复领取仍由用例拒绝。
    public void Render(MatchPageViewModel view)
    {
        Clear(); _summary.Text = view.Message;
        if (_log.Text != view.BattleLog) _log.Hide();
        _log.Text = view.BattleLog;
        var height = Mathf.Clamp(Size.Y - 82, 60, 180);
        foreach (var reward in view.Rewards)
        {
            var width = reward.Card is null ? 220 : height * (int)reward.Card.Size / 2 + 112;
            var row = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = MouseFilterEnum.Ignore };
            _rewards.AddChild(row);
            var inset = 44f;
            if (reward.Card is not null)
            {
                var card = new CardItemView { Name = $"RewardCard{reward.Index}", Size = new Vector2(width - 112, height) };
                row.AddChild(card); card.Render(reward.Card, _adapter);
                card.DetailsRequested += ShowDetails; _cards.Add(card); inset = card.Size.X + 12;
            }
            else
            {
                var icon = new TextureRect { Texture = MatchTheme.Icon("skills"), Position = new Vector2(8, 28),
                    Size = new Vector2(28, 28), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
                row.AddChild(icon);
                var name = new Label { Text = reward.Text, Position = new Vector2(44, 8), Size = new Vector2(172, 24),
                    ClipText = true, TooltipText = reward.Text, MouseFilter = MouseFilterEnum.Ignore };
                name.AddThemeFontSizeOverride("font_size", 16); row.AddChild(name);
            }
            var claim = new Button { Name = $"Claim{reward.Index}", Text = reward.Card is null ? "领取技能" : "领取",
                Icon = MatchTheme.Icon("rewards"), TooltipText = reward.Text + "\n" + reward.Details,
                Position = new Vector2(inset, height / 2 - 16), Size = new Vector2(width - inset, 32), ClipText = true };
            claim.AddThemeFontSizeOverride("font_size", 16); claim.AddThemeConstantOverride("icon_max_width", 16);
            row.AddChild(claim);
            var match = view.Player?.MatchId ?? Guid.Empty;
            Action handler = () => ClaimRequested?.Invoke(match, reward.Index, reward.Revision);
            claim.Pressed += handler; _bindings.Add((claim, handler));
        }
        LayoutView();
    }

    // 日志只改变可见性，不再次结算；关闭后恢复奖励区输入。
    public void ToggleLog()
    {
        if (_log.Text.Length == 0) return;
        _log.Visible = !_log.Visible; _scroll.Visible = !_log.Visible;
    }

    public override void _ExitTree() { Resized -= LayoutView; Clear(); }

    private void ShowDetails(CardSnapshot card) => DetailsRequested?.Invoke(card);

    private void Clear()
    {
        foreach (var (button, handler) in _bindings) button.Pressed -= handler;
        foreach (var card in _cards) card.DetailsRequested -= ShowDetails;
        foreach (var child in _rewards.GetChildren()) { _rewards.RemoveChild(child); child.QueueFree(); }
        _bindings.Clear(); _cards.Clear();
    }

    private void LayoutView()
    {
        _summary.Size = new Vector2(Mathf.Max(1, Size.X - 88), 54);
        _scroll.Position = new Vector2(12, 70); _scroll.Size = new Vector2(Mathf.Max(1, Size.X - 24), Mathf.Max(1, Size.Y - 74));
        _scroll.Visible = !_log.Visible;
        _log.Position = new Vector2(12, 70); _log.Size = _scroll.Size;
        var height = Mathf.Clamp(Size.Y - 82, 60, 180);
        foreach (Control row in _rewards.GetChildren())
        {
            var card = row.GetChildren().OfType<CardItemView>().SingleOrDefault();
            var claim = row.GetChildren().OfType<Button>().Single(child => child is not CardItemView);
            var width = card is null ? 220 : height * card.Size.X / card.Size.Y + 112;
            var inset = card is null ? 44 : width - 100;
            row.CustomMinimumSize = new Vector2(width, height);
            if (card is not null) card.Size = new Vector2(width - 112, height);
            claim.Position = new Vector2(inset, height / 2 - 16); claim.Size = new Vector2(width - inset, 32);
        }
    }
}
