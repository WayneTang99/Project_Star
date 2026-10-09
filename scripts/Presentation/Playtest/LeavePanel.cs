using System;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 固定操作意图，不承担对局流程。
public enum MatchAction { Refresh, Battle, Continue, Reward, Reset, Verification, Showcase, Pause, Speed, Skip, Developer, Log }

// 右侧操作区保留按钮节点，订阅仅在进入场景时建立。
public sealed partial class LeavePanel : HFlowContainer
{
    public event Action<MatchAction>? Requested;
    private readonly Dictionary<MatchAction, Button> _buttons = new();
    private readonly Dictionary<MatchAction, Action> _handlers = new();
    private MatchPageViewModel? _view;
    private bool _expanded;
    private Button _stageContinue = null!;
    public override void _Ready()
    {
        foreach (var intent in Enum.GetValues<MatchAction>())
        {
            var button = new Button { Name = intent.ToString(), ClipText = true };
            button.AddThemeFontSizeOverride("font_size", 10);
            button.AddThemeConstantOverride("icon_max_width", 10);
            Action handler = () =>
            {
                if (button.Disabled || !button.Visible) return;
                if (intent == MatchAction.Developer)
                { _expanded = !_expanded; if (_view is not null) Render(_view); }
                else Requested?.Invoke(intent);
            };
            _buttons.Add(intent, button); _handlers.Add(intent, handler);
            AddChild(button); button.Pressed += handler;
        }
        MoveChild(_buttons[MatchAction.Pause], 0);
        MoveChild(_buttons[MatchAction.Speed], 1);
        MoveChild(_buttons[MatchAction.Skip], 2);
        MoveChild(_buttons[MatchAction.Developer], 7);
        AddThemeConstantOverride("h_separation", 9); AddThemeConstantOverride("v_separation", 4);
        _stageContinue = new Button { Name = "Leave", Text = "离开 →", CustomMinimumSize = new Vector2(50, 26), Visible = false };
        _stageContinue.AddThemeFontSizeOverride("font_size", 10); AddChild(_stageContinue); _stageContinue.Pressed += LeaveStage;
        MoveChild(_buttons[MatchAction.Refresh], 0); MoveChild(_stageContinue, 1);
        foreach (var intent in new[] { MatchAction.Refresh, MatchAction.Developer })
        {
            var plate = MatchTheme.Surface(new Color("1e3128"), new Color("6b775a"));
            plate.ContentMarginLeft = plate.ContentMarginRight = 9; plate.ContentMarginTop = plate.ContentMarginBottom = 6;
            _buttons[intent].AddThemeStyleboxOverride("normal", plate);
        }
    }

    // 条件来自页面模型，不在显示期间执行命令。
    public void Render(MatchPageViewModel view)
    {
        if (_view?.Player?.MatchId != view.Player?.MatchId) _expanded = false;
        _view = view;
        _stageContinue.Visible = view.Page == MatchPage.Shop; _stageContinue.Disabled = !view.Continue.Enabled;
        Set(MatchAction.Refresh, view.Refresh); Set(MatchAction.Battle, view.Battle);
        Set(MatchAction.Continue, view.Continue); Set(MatchAction.Reward, view.Reward with { Text = $"战利品 ({view.Rewards.Count})" });
        Set(MatchAction.Pause, new UiAction(view.Playback?.Paused == true ? "继续播放" : "暂停", view.Playback is not null));
        Set(MatchAction.Speed, new UiAction($"{view.Playback?.Speed ?? 1}×", view.Playback is not null));
        Set(MatchAction.Skip, new UiAction("跳过", view.Playback is not null));
        Set(MatchAction.Reset, new UiAction("重开", Visible: _expanded && view.Playback is null));
        Set(MatchAction.Developer, new UiAction(_expanded ? "收起 ▴" : "•••", Visible: view.Playback is null));
        Set(MatchAction.Verification, new UiAction("规则验证", Visible: _expanded && view.Playback is null));
        Set(MatchAction.Showcase, new UiAction("组件展示", Visible: _expanded && view.Playback is null));
        Set(MatchAction.Log, new UiAction("展开 / 收起战斗日志",
            Visible: _expanded && view.Page is MatchPage.BattleResult or MatchPage.MatchEnded));
    }

    public override void _ExitTree()
    {
        _stageContinue.Pressed -= LeaveStage;
        foreach (var (intent, button) in _buttons) button.Pressed -= _handlers[intent];
    }
    private void LeaveStage() { if (_view?.Continue.Enabled == true) Requested?.Invoke(MatchAction.Continue); }

    // 主行动保留原有意图与订阅，仅移到英雄栏右侧。
    public void SetFooterHost(Control host)
    {
        foreach (var intent in new[] { MatchAction.Battle, MatchAction.Continue, MatchAction.Reward })
        {
            _buttons[intent].Reparent(host);
            MatchTheme.Accent(_buttons[intent]);
        }
    }

    private void Set(MatchAction intent, UiAction action)
    {
        var button = _buttons[intent]; button.Text = action.Text; button.Visible = action.Visible;
        if (intent == MatchAction.Continue && _view?.Page != MatchPage.MatchEnded) button.Text = "继续旅程　→";
        if (intent == MatchAction.Refresh)
        {
            var amount = System.Text.RegularExpressions.Regex.Match(action.Text, @"\d+");
            button.Text = amount.Success ? $"刷新 · {amount.Value}" : "刷新";
        }
        var key = intent switch
        {
            MatchAction.Refresh => new StringName("refresh"), MatchAction.Battle => new StringName("battle"),
            MatchAction.Continue => new StringName("continue"), MatchAction.Reward => new StringName("rewards"),
            MatchAction.Pause => new StringName(_view?.Playback?.Paused == true ? "play" : "pause"),
            _ => new StringName(""),
        };
        button.Icon = key.IsEmpty ? null : MatchTheme.Icon(key);
        button.CustomMinimumSize = new Vector2(intent is MatchAction.Battle or MatchAction.Continue or MatchAction.Reward ? 0 : 40,
            intent is MatchAction.Battle or MatchAction.Continue or MatchAction.Reward ? 36 : 26);
        if (intent == MatchAction.Refresh) button.ClipText = false;
        button.Disabled = !action.Enabled; button.TooltipText = action.Text + "\n" + action.Reason;
    }
}
