using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 战斗阶段使用 HTML 的敌方肖像、说明和资源摘要，指令由外部用例执行。
public sealed partial class BattleStageView : Control
{
    public event Action<MatchAction>? Requested;
    public event Action? InspectRequested;
    private TextureRect _portrait = null!;
    private Label _name = null!;
    private Label _eyebrow = null!;
    private Label _title = null!;
    private Label _description = null!;
    private Label _vs = null!;
    private ResourceBar _health = null!;
    private HBoxContainer _controls = null!;
    private Button _play = null!;
    private Button _speed = null!;
    private Button _skip = null!;
    private Button _inspect = null!;
    private bool _playing;
    public override void _Ready()
    {
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore }; AddChild(_portrait);
        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_name, 12); _name.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("11211adb"), Colors.Transparent)); AddChild(_name);
        _vs = new Label { Text = "VS" }; MatchTheme.Text(_vs, 30, new Color("a18750b3"), true); AddChild(_vs);
        _eyebrow = new Label(); MatchTheme.Text(_eyebrow, 10, MatchTheme.Gold, spacing: 4); AddChild(_eyebrow);
        _title = new Label(); MatchTheme.Text(_title, 24, new Color("e1cfab"), spacing: 5); AddChild(_title);
        _description = new Label { Text = "伤害、护甲与中毒，组合出你的制胜之道。", ClipText = true }; MatchTheme.Text(_description, 11, new Color("9aaa9b")); AddChild(_description);
        _health = new ResourceBar("EnemyHealth", new Color("86b25d")); _health.SetDisplayHeight(20); AddChild(_health);
        _controls = new HBoxContainer { Name = "Controls" }; AddChild(_controls);
        _play = new Button(); MatchTheme.Accent(_play); _controls.AddChild(_play); _play.Pressed += Play;
        _speed = new Button(); _speed.AddThemeFontSizeOverride("font_size", 11); _controls.AddChild(_speed); _speed.Pressed += Speed;
        _skip = new Button { Text = "跳过" }; _skip.AddThemeFontSizeOverride("font_size", 11); _controls.AddChild(_skip); _skip.Pressed += Skip;
        _inspect = new Button { Name = "Inspect", Text = "查看敌方阵容" }; _inspect.AddThemeFontSizeOverride("font_size", 10); _controls.AddChild(_inspect); _inspect.Pressed += Inspect;
        Resized += LayoutView;
    }
    // 使用当前敌方快照与回放数值，展示刷新不推进战斗。
    public void Render(MatchPageViewModel view)
    {
        _playing = view.Playback is not null;
        var path = !view.ContextIllustration.IsEmpty ? view.ContextIllustration : view.Enemy?.Hero?.Illustration ?? new StringName("");
        _portrait.Texture = !path.IsEmpty && ResourceLoader.Exists(path.ToString()) ? GD.Load<Texture2D>(path.ToString()) : null;
        _name.Text = view.Enemy?.Hero?.DisplayName ?? view.Title;
        _eyebrow.Text = $"战斗 · 第 {view.DisplayRound} 轮";
        _title.Text = _playing ? view.Playback!.Paused ? "交锋暂停" : "交锋中" : "准备迎战";
        var maximum = view.Playback?.State.Opponent.MaxHealth ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth) ?? 1;
        _health.Render("生命", view.Playback?.State.Opponent.Health ?? maximum, maximum);
        _play.Text = !_playing ? "开始战斗" : view.Playback!.Paused ? "继续" : "暂停";
        _play.Disabled = !_playing && !view.Battle.Enabled; _speed.Visible = _skip.Visible = _playing; _speed.Text = $"{view.Playback?.Speed ?? 1}×";
        LayoutView();
    }
    public override void _ExitTree() { Resized -= LayoutView; _play.Pressed -= Play; _speed.Pressed -= Speed; _skip.Pressed -= Skip; _inspect.Pressed -= Inspect; }
    private void Play() => Requested?.Invoke(_playing ? MatchAction.Pause : MatchAction.Battle);
    private void Speed() => Requested?.Invoke(MatchAction.Speed);
    private void Skip() => Requested?.Invoke(MatchAction.Skip);
    private void Inspect() => InspectRequested?.Invoke();
    private void LayoutView()
    {
        var width = Mathf.Min(460, Size.X - 310); var left = Size.X - width - 22; var top = (Size.Y - 164) / 2;
        _portrait.Position = new Vector2(22, (Size.Y - 150) / 2); _portrait.Size = new Vector2(110, 150);
        _name.Position = _portrait.Position + new Vector2(8, 112); _name.Size = new Vector2(94, 30);
        _vs.Position = new Vector2((left + 132) / 2 - 20, Size.Y / 2 - 20); _vs.Size = new Vector2(55, 40);
        _eyebrow.Position = new Vector2(left, top); _eyebrow.Size = new Vector2(width, 16);
        _title.Position = new Vector2(left, top + 24); _title.Size = new Vector2(width, 34);
        _description.Position = new Vector2(left, top + 71); _description.Size = new Vector2(width, 20);
        _health.Position = new Vector2(left, top + 111); _health.Size = new Vector2(width, 20);
        _controls.Position = new Vector2(left, top + 145); _controls.Size = new Vector2(width, 36);
        QueueRedraw();
    }
    public override void _Draw() { if (_portrait is not null) DrawRect(_portrait.GetRect().Grow(1), new Color("ad9569"), false, 2); }
}
