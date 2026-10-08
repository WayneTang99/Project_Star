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
    private CardLevelGem _level = null!;
    private HBoxContainer _resources = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    private Label _armor = null!;

    public override void _Ready()
    {
        _title = new Label { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_title); _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _title.AddThemeFontSizeOverride("font_size", 16);
        _level = new CardLevelGem { Name = "EncounterLevelCrystal", Size = new Vector2(26, 34), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_level); _level.Hide();
        _resources = new HBoxContainer { Name = "EnemyResources" }; AddChild(_resources);
        _health = new ResourceBar("EnemyHealth", new Color("86bf8c")) { CustomMinimumSize = new Vector2(112, 24) };
        _mana = new ResourceBar("EnemyMana", new Color("88c4eb")) { CustomMinimumSize = new Vector2(100, 24) };
        _resources.AddChild(_health); _resources.AddChild(_mana);
        var armor = MatchTheme.Stat("armor", "", "敌方护甲"); _resources.AddChild(armor); _armor = armor.GetChild<Label>(1);
        Resized += LayoutTitle; LayoutTitle();
    }

    // 准备阶段使用英雄快照，回放阶段使用冻结的战斗快照。
    public void Render(string title, int encounterLevel = 0, HeroSnapshot? enemy = null, HeroBattleSnapshot? battle = null)
    {
        _title.Text = title; TooltipText = encounterLevel > 0 ? $"{title} · 等级 {encounterLevel}" : title;
        _level.Visible = encounterLevel > 0; _level.SetLevel(encounterLevel);
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

    public override void _ExitTree() => Resized -= LayoutTitle;

    private void LayoutTitle()
    {
        _level.Position = new Vector2(0, 1);
        _title.OffsetLeft = _level.Visible ? 34 : 0;
        _title.OffsetBottom = _resources.Visible ? -23 : 0;
        _resources.Position = new Vector2(_title.OffsetLeft, 19);
        _resources.Size = new Vector2(Mathf.Max(1, Size.X - _title.OffsetLeft), 24);
        _title.AddThemeFontSizeOverride("font_size", _resources.Visible ? 13 : 16);
    }
}
