using System.Collections.Generic;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 阶段标题与敌方资源只展示同次捕获，等级晶体不重复进入内容区。
public sealed partial class ContextPortrait : Control
{
    private Label _title = null!;
    private VBoxContainer _resources = null!;
    private TextureRect _art = null!;
    private ColorRect _shade = null!;
    private Label _message = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    private Label _armor = null!;
    private Label _eyebrow = null!;

    public override void _Ready()
    {
        _art = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, Modulate = new Color(.65f, .72f, .61f) };
        AddChild(_art); _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _shade = new ColorRect { Color = new Color(.04f, .09f, .07f, .74f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_shade); _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _art.Modulate = Colors.White; _shade.Color = Colors.Transparent;
        var veil = new TextureRect { Texture = GD.Load<Texture2D>("res://art/ui/html/stage-shade.svg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _eyebrow = new Label { ClipText = true }; MatchTheme.Text(_eyebrow, 10, MatchTheme.Gold, spacing: 4); AddChild(_eyebrow);
        _title = new Label { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_title); _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _title.AddThemeColorOverride("font_color", new Color("f0deb5"));
        _message = new Label { ClipText = true, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(_message, 11, new Color("c1c7b3")); _message.AddThemeConstantOverride("line_spacing", 7);
        _message.AddThemeColorOverride("font_color", MatchTheme.Muted); AddChild(_message);
        _resources = new VBoxContainer { Name = "EnemyResources" }; AddChild(_resources);
        _resources.AddThemeConstantOverride("separation", 4);
        _health = new ResourceBar("EnemyHealth", new Color("86bf8c")) { CustomMinimumSize = new Vector2(112, 24) };
        _mana = new ResourceBar("EnemyMana", new Color("88c4eb")) { CustomMinimumSize = new Vector2(100, 24) };
        _health.SetDisplayHeight(20); _mana.SetDisplayHeight(20);
        _resources.AddChild(_health); _resources.AddChild(_mana);
        var armor = MatchTheme.Stat("armor", "", "敌方护甲"); _resources.AddChild(armor); _armor = armor.GetChild<Label>(1);
        Resized += LayoutTitle; LayoutTitle();
    }

    // 准备阶段使用英雄快照，回放阶段使用冻结的战斗快照。
    public void Render(string title, int encounterLevel = 0, HeroSnapshot? enemy = null, HeroBattleSnapshot? battle = null)
    {
        _title.Text = title; TooltipText = encounterLevel > 0 ? $"{title} · 等级 {encounterLevel}" : title;
        LevelPresentation.Frame(this, encounterLevel);
        _resources.Visible = enemy is not null || battle is not null;
        if (_resources.Visible)
        {
            var maxHealth = battle?.MaxHealth ?? enemy!.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth);
            _health.Render("生命", battle?.Health ?? maxHealth, maxHealth);
            _mana.Render("魔法", battle?.Mana ?? enemy!.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana),
                battle?.MaxMana ?? enemy!.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana));
            _armor.Text = (battle?.Armor ?? enemy!.CombatValues.GetValueOrDefault(GameAttributeKeys.Armor)).ToString();
        }
        LayoutTitle();
    }

    // 阶段插画与说明从当前只读页面模型加载，不按卡牌或遭遇key匹配资源。
    public void SetContext(StringName illustration, string message)
    {
        _art.Texture = !illustration.IsEmpty && ResourceLoader.Exists(illustration.ToString())
            ? GD.Load<Texture2D>(illustration.ToString()) : null;
        _message.Text = message; _message.TooltipText = message;
        LayoutTitle();
    }

    // 阶段眉题显示访问时回合及遭遇类别。
    public void SetEyebrow(string text) { _eyebrow.Text = text; LayoutTitle(); }

    public override void _ExitTree() => Resized -= LayoutTitle;

    private void LayoutTitle()
    {
        _eyebrow.Position = new Vector2(24, Size.Y / 2 - 77); _eyebrow.Size = new Vector2(Size.X - 48, 18);
        _title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _title.Position = new Vector2(24, Size.Y / 2 - 49); _title.Size = new Vector2(Mathf.Max(1, Size.X - 48), 43);
        MatchTheme.Text(_title, _resources.Visible ? 20 : 27, new Color("f0deb5"), spacing: 3);
        _message.Position = new Vector2(24, Size.Y / 2 + 4);
        _message.Size = new Vector2(Mathf.Max(1, Size.X - 48), 40);
        _message.Visible = true; _resources.Hide();
        _resources.Position = new Vector2(12, 83); _resources.Size = new Vector2(Mathf.Max(1, Size.X - 24), 73);
    }
}
