using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Playtest;

// 左下英雄身份区域，正式画像接入前使用名称占位。
public enum HeroSection { Skills, Sets, Rewards }

// 英雄快照与技能、套装、奖励入口共用左下区域。
public sealed partial class PlayerHeroPanel : VBoxContainer
{
    public event Action<HeroSection>? SectionRequested;
    private Label _name = null!;
    private Label _battle = null!;
    private HFlowContainer _buttons = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    public override void _Ready()
    {
        _name = new Label { ClipText = true }; AddChild(_name);
        _health = new ResourceBar("HealthBar", new Color("86bf8c")); AddChild(_health);
        _mana = new ResourceBar("ManaBar", new Color("88c4eb")); AddChild(_mana);
        _battle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false }; AddChild(_battle);
        _buttons = new HFlowContainer(); AddChild(_buttons);
        AddEntry("技能", HeroSection.Skills); AddEntry("套装", HeroSection.Sets); AddEntry("奖励", HeroSection.Rewards);
    }
    // 使用快照中的英雄身份，不持有实体引用。
    public void Render(HeroSnapshot? hero)
    {
        _name.Text = hero is null ? "英雄" : PlaytestText.FormatHeroName(hero.DisplayName, hero.Title);
        _buttons.Visible = _health.Visible = _mana.Visible = hero is not null;
        if (hero is null) return;
        var health = hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth);
        _health.Render("生命", health, health);
        _mana.Render("魔法", hero.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana),
            hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana));
    }
    // 战斗HUD只显示播放中的英雄数值，播放结束恢复构筑入口。
    public void RenderBattle(HeroBattleSnapshot? hero)
    {
        _battle.Visible = hero is not null;
        if (hero is not null)
        {
            _health.Show(); _mana.Show();
            _health.Render("生命", hero.Health, hero.MaxHealth);
            _mana.Render("魔法", hero.Mana, hero.MaxMana);
            _battle.Text = $"护甲 {hero.Armor}\n灼伤 {hero.Burn} · 中毒 {hero.Poison}";
            _buttons.Hide();
        }
    }
    private void AddEntry(string text, HeroSection section)
    {
        var button = new Button { Text = text }; _buttons.AddChild(button);
        button.Pressed += () => SectionRequested?.Invoke(section);
    }
}
