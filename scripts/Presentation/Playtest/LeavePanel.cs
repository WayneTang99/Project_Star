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
    public override void _Ready()
    {
        foreach (var intent in Enum.GetValues<MatchAction>())
        {
            var button = new Button { Name = intent.ToString(), ClipText = true };
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeConstantOverride("icon_max_width", 16);
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
    }

    // 条件来自页面模型，不在显示期间执行命令。
    public void Render(MatchPageViewModel view)
    {
        if (_view?.Player?.MatchId != view.Player?.MatchId) _expanded = false;
        _view = view;
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
        foreach (var (intent, button) in _buttons) button.Pressed -= _handlers[intent];
    }

    private void Set(MatchAction intent, UiAction action)
    {
        var button = _buttons[intent]; button.Text = action.Text; button.Visible = action.Visible;
        var key = intent switch
        {
            MatchAction.Refresh => new StringName("refresh"), MatchAction.Battle => new StringName("battle"),
            MatchAction.Continue => new StringName("continue"), MatchAction.Reward => new StringName("rewards"),
            MatchAction.Pause => new StringName(_view?.Playback?.Paused == true ? "play" : "pause"),
            _ => new StringName(""),
        };
        button.Icon = key.IsEmpty ? null : MatchTheme.Icon(key);
        button.CustomMinimumSize = new Vector2(Mathf.Max(40, button.GetThemeFont("font").GetStringSize(action.Text,
            fontSize: button.GetThemeFontSize("font_size")).X + 24 + (button.Icon is null ? 0 : 20)), 34);
        button.Disabled = !action.Enabled; button.TooltipText = action.Text + "\n" + action.Reason;
    }
}
