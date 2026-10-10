using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Playtest;

// 战斗阶段使用 HTML 的敌方肖像、说明和资源摘要，指令由外部用例执行。
public sealed partial class BattleStageView : Control
{
    public event Action<MatchAction>? Requested;
    private TextureRect _portrait = null!;
    private Label _name = null!;
    private Label _eyebrow = null!;
    private Label _title = null!;
    private Label _description = null!;
    private Label _vs = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    private HeroBattleStatusView _statuses = null!;
    private int _level;
    private HBoxContainer _controls = null!;
    private Button _play = null!;
    private Button _speed = null!;
    private Button _skip = null!;
    private bool _playing;
    public override void _Ready()
    {
        _portrait = new TextureRect { Name = "EnemyPortrait", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, MouseFilter = MouseFilterEnum.Pass }; AddChild(_portrait);
        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_name, 12); _name.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("11211adb"), Colors.Transparent)); AddChild(_name);
        _vs = new Label { Text = "VS" }; MatchTheme.Text(_vs, 30, new Color("a18750b3"), true); AddChild(_vs);
        _eyebrow = new Label(); MatchTheme.Text(_eyebrow, 10, MatchTheme.Gold, spacing: 4); AddChild(_eyebrow);
        _title = new Label(); MatchTheme.Text(_title, 24, new Color("e1cfab"), spacing: 5); AddChild(_title);
        _description = new Label { Text = "伤害、护甲与中毒，组合出你的制胜之道。", ClipText = true }; MatchTheme.Text(_description, 11, new Color("9aaa9b")); AddChild(_description);
        _health = new ResourceBar("EnemyHealth", new Color("86b25d")); _health.SetDisplayHeight(20); AddChild(_health);
        _mana = new ResourceBar("EnemyMana", new Color("438fa2")); _mana.SetDisplayHeight(10); AddChild(_mana);
        _statuses = new HeroBattleStatusView { Name = "EnemyStatuses" }; AddChild(_statuses);
        _controls = new HBoxContainer { Name = "Controls" }; AddChild(_controls);
        _play = new Button(); MatchTheme.Accent(_play); _controls.AddChild(_play); _play.Pressed += Play;
        _speed = new Button(); _speed.AddThemeFontSizeOverride("font_size", 11); _controls.AddChild(_speed); _speed.Pressed += Speed;
        _skip = new Button { Text = "跳过" }; _skip.AddThemeFontSizeOverride("font_size", 11); _controls.AddChild(_skip); _skip.Pressed += Skip;
        Resized += LayoutView;
    }
    // 使用当前敌方快照与回放数值，展示刷新不推进战斗。
    public void Render(MatchPageViewModel view)
    {
        _playing = view.Playback is not null;
        _level = view.EncounterLevel;
        var path = !view.ContextIllustration.IsEmpty ? view.ContextIllustration : view.Enemy?.Hero?.Illustration ?? new StringName("");
        _portrait.Texture = !path.IsEmpty && ResourceLoader.Exists(path.ToString()) ? GD.Load<Texture2D>(path.ToString()) : null;
        _name.Text = view.Enemy?.Hero?.DisplayName ?? view.Title;
        _eyebrow.Text = $"战斗 · 第 {view.DisplayRound} 轮";
        _title.Text = _playing ? view.Playback!.Paused ? "交锋暂停" : "交锋中" : "准备迎战";
        var maximum = view.Playback?.State.Opponent.MaxHealth ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth) ?? 1;
        _health.Render("生命", view.Playback?.State.Opponent.Health ?? maximum, maximum);
        _mana.Render("魔法", view.Playback?.State.Opponent.Mana ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana) ?? 0,
            view.Playback?.State.Opponent.MaxMana ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana) ?? 0);
        _statuses.Render(view.Playback?.State.Opponent, view.Playback, SideId.Opponent);
        _portrait.TooltipText = _level > 0 ? $"{_name.Text} · 等级 {_level}" : _name.Text;
        LevelPresentation.Frame(_portrait, _level);
        _play.Text = !_playing ? "开始战斗" : view.Playback!.Paused ? "继续" : "暂停";
        _play.Disabled = !_playing && !view.Battle.Enabled; _speed.Visible = _skip.Visible = _playing; _speed.Text = $"{view.Playback?.Speed ?? 1}×";
        LayoutView();
    }
    public override void _ExitTree() { Resized -= LayoutView; _play.Pressed -= Play; _speed.Pressed -= Speed; _skip.Pressed -= Skip; }
    private void Play() => Requested?.Invoke(_playing ? MatchAction.Pause : MatchAction.Battle);
    private void Speed() => Requested?.Invoke(MatchAction.Speed);
    private void Skip() => Requested?.Invoke(MatchAction.Skip);
    private void LayoutView()
    {
        var left = 185f; var width = Mathf.Max(1, Size.X - left); var top = 8f;
        _portrait.Position = new Vector2(0, (Size.Y - 150) / 2); _portrait.Size = new Vector2(110, 150);
        _name.Position = _portrait.Position + new Vector2(8, 112); _name.Size = new Vector2(94, 30);
        _vs.Position = new Vector2(130, Size.Y / 2 - 20); _vs.Size = new Vector2(55, 40);
        _eyebrow.Position = new Vector2(left, top); _eyebrow.Size = new Vector2(width, 16);
        _title.Position = new Vector2(left, top + 24); _title.Size = new Vector2(width, 34);
        _description.Position = new Vector2(left, top + 71); _description.Size = new Vector2(width, 20);
        _health.Position = new Vector2(left, top + 98); _health.Size = new Vector2(width, 20);
        _mana.Position = new Vector2(left, top + 125); _mana.Size = new Vector2(width, 10);
        _statuses.Position = new Vector2(left, top + 150); _statuses.Size = new Vector2(width, 25);
        _controls.Position = new Vector2(left, top + 185); _controls.Size = new Vector2(width, 36);
        QueueRedraw();
    }
    public override void _Draw() { if (_portrait is not null && _level == 0) DrawRect(_portrait.GetRect().Grow(1), new Color("ad9569"), false, 2); }
}
