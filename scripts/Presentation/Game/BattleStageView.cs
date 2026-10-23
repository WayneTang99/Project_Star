using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Game;

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
    private Label _manaRegen = null!;
    private HeroBattleStatusView _statuses = null!;
    private int _level;
    private Label _armorValue = null!;
    private Label _regenValue = null!;
    private HBoxContainer _controls = null!;
    private Button _play = null!;
    private Button _speed = null!;
    private Button _skip = null!;
    private bool _playing;
    public override void _Ready()
    {
        _portrait = new TextureRect { Name = "EnemyPortrait", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, MouseFilter = MouseFilterEnum.Pass }; AddChild(_portrait);
        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_name, 12); _name.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("302820db"), Colors.Transparent)); AddChild(_name);
        _vs = new Label { Text = "VS" }; MatchTheme.Text(_vs, 30, new Color("a18750b3"), true); AddChild(_vs);
        _eyebrow = new Label(); MatchTheme.Text(_eyebrow, 10, MatchTheme.Gold, spacing: 4); AddChild(_eyebrow);
        _title = new Label(); MatchTheme.Text(_title, 24, new Color("e1cfab"), spacing: 5); AddChild(_title);
        _description = new Label { Text = "伤害、护甲与中毒，组合出你的制胜之道。", ClipText = true }; MatchTheme.Text(_description, 11, new Color("9aaa9b")); AddChild(_description);
        _health = new ResourceBar("EnemyHealth", new Color("86b25d")); _health.SetDisplayHeight(24); AddChild(_health);
        _mana = new ResourceBar("EnemyMana", new Color("438fa2")); _mana.SetDisplayHeight(24); AddChild(_mana);
        _manaRegen = new Label { Name = "ManaRegen", Position = new Vector2(180, 0), Size = new Vector2(57, 24),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, TooltipText = "敌方魔法再生" };
        MatchTheme.Text(_manaRegen, 15, AttributePalette.Find(GameAttributeKeys.ManaRegen), true, true); _mana.AddChild(_manaRegen);
        _armorValue = new Label { Name = "ArmorValue", Position = new Vector2(7, 0), Size = new Vector2(55, 24), TooltipText = "敌方当前护甲", VerticalAlignment = VerticalAlignment.Center };
        MatchTheme.Text(_armorValue, 15, AttributePalette.Find(GameAttributeKeys.Armor), true, true); _health.AddChild(_armorValue);
        _regenValue = new Label { Name = "RegenValue", Position = new Vector2(180, 0), Size = new Vector2(57, 24), HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, TooltipText = "敌方每秒生命再生" };
        MatchTheme.Text(_regenValue, 15, AttributePalette.Find(GameAttributeKeys.HealthRegen), true, true); _health.AddChild(_regenValue);
        _statuses = new HeroBattleStatusView { Name = "EnemyStatuses", ShowArmor = false }; AddChild(_statuses);
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
        var path = view.Enemy?.Hero?.Illustration ?? new StringName("");
        if (path.IsEmpty) path = view.ContextIllustration;
        _portrait.Texture = PlayerHeroPanel.PortraitTexture(view.Enemy?.Hero)
            ?? (!path.IsEmpty && ResourceLoader.Exists(path.ToString()) ? GD.Load<Texture2D>(path.ToString()) : null);
        _name.Text = view.Enemy?.Hero?.DisplayName ?? view.Title;
        _portrait.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/PortraitClip.gdshader") };
        _eyebrow.Text = $"战斗 · 第 {view.DisplayRound} 轮";
        _title.Text = _playing ? view.Playback!.Paused ? "交锋暂停" : "交锋中" : "准备迎战";
        var maximum = view.Playback?.State.Opponent.MaxHealth ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth) ?? 1;
        _health.Render("生命", view.Playback?.State.Opponent.Health ?? maximum, maximum);
        _mana.Render("魔法", view.Playback?.State.Opponent.Mana ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana) ?? 0,
            view.Playback?.State.Opponent.MaxMana ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana) ?? 0);
        _armorValue.Text = (view.Playback?.State.Opponent.Armor ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.Armor) ?? 0).ToString();
        _regenValue.Text = "+" + (view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.HealthRegen) ?? 0);
        _manaRegen.Text = "+" + (view.Playback?.State.Opponent.ManaRegen ?? view.Enemy?.Hero?.CombatValues.GetValueOrDefault(GameAttributeKeys.ManaRegen) ?? 0);
        _statuses.Render(view.Playback?.State.Opponent, view.Playback, SideId.Opponent);
        _portrait.TooltipText = _level > 0 ? $"{_name.Text} · 等级 {_level}" : _name.Text;

        _play.Text = !_playing ? "开始战斗" : view.Playback!.Paused ? "继续" : "暂停";
        _controls.Visible = _playing;
        _play.Disabled = !_playing && !view.Battle.Enabled; _speed.Visible = _skip.Visible = _playing; _speed.Text = $"{view.Playback?.Speed ?? 1}×";
        LayoutView();
    }
    public override void _ExitTree() { Resized -= LayoutView; _play.Pressed -= Play; _speed.Pressed -= Speed; _skip.Pressed -= Skip; }
    private void Play() => Requested?.Invoke(_playing ? MatchAction.Pause : MatchAction.Battle);
    private void Speed() => Requested?.Invoke(MatchAction.Speed);
    private void Skip() => Requested?.Invoke(MatchAction.Skip);
    private void LayoutView()
    {
        var center = Size.X / 2;
        _portrait.Position = new Vector2(center - 45, 0); _portrait.Size = new Vector2(90, 90);
        _name.Hide(); _eyebrow.Hide(); _description.Hide(); _vs.Hide(); _title.Hide();
        _health.Position = new Vector2(center - 320, 28); _health.Size = new Vector2(244, 24);
        _mana.Position = new Vector2(center + 76, 28); _mana.Size = new Vector2(244, 24);
        _statuses.Position = new Vector2(center + 76, 62); _statuses.Size = new Vector2(244, 25);
        _controls.Position = new Vector2(center - 320, 64); _controls.Size = new Vector2(244, 32);
        QueueRedraw();
    }

    public override void _Draw() { if (_portrait is not null) MatchTheme.DrawPortraitMount(this, _portrait.GetRect(), true); }
}
