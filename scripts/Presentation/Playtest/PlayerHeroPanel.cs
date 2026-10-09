using System;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Playtest;

// 左下英雄身份区域及详情入口分类。
public enum HeroSection { Skills, Sets, Rewards }

// 底部横向英雄栏展示身份、资源与技能、套装、奖励入口。
public sealed partial class PlayerHeroPanel : Control
{
    public event Action<HeroSection>? SectionRequested;
    private Label _name = null!;
    private Label _subtitle = null!;
    private HBoxContainer _battle = null!;
    private Label _armor = null!;
    private Label _burn = null!;
    private Label _poison = null!;
    private HFlowContainer _buttons = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    private TextureRect _portrait = null!;
    private Control _identity = null!;
    private Label _level = null!;
    public override void _Ready()
    {
        var identity = new Control { Name = "Identity", MouseFilter = MouseFilterEnum.Ignore }; AddChild(identity);
        _identity = identity;
        _portrait = new TextureRect { Name = "Portrait", CustomMinimumSize = new Vector2(64, 64),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore };
        identity.AddChild(_portrait);
        _name = new Label { Name = "HeroName", ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 36), VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart }; identity.AddChild(_name);
        _name.AddThemeFontSizeOverride("font_size", 22);
        _subtitle = new Label { Name = "Subtitle", ClipText = true }; identity.AddChild(_subtitle);
        _subtitle.AddThemeFontSizeOverride("font_size", 16);
        _subtitle.AddThemeColorOverride("font_color", MatchTheme.Muted);
        _level = new Label { Name = "Level", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _level.AddThemeColorOverride("font_color", new Color("defaff"));
        _level.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("325b67"), new Color("8fc5cc")));
        identity.AddChild(_level);
        _health = new ResourceBar("HealthBar", new Color("729c51")); AddChild(_health);
        _mana = new ResourceBar("ManaBar", new Color("438fa2")); AddChild(_mana);
        _health.SetDisplayHeight(22); _mana.SetDisplayHeight(18);
        _battle = new HBoxContainer { Name = "BattleResources", Visible = false }; AddChild(_battle);
        var armor = MatchTheme.Stat("armor", "", "护甲"); var burn = MatchTheme.Stat("burn", "", "灼伤");
        var poison = MatchTheme.Stat("poison", "", "中毒");
        _battle.AddChild(armor); _battle.AddChild(burn); _battle.AddChild(poison);
        _armor = armor.GetChild<Label>(1); _burn = burn.GetChild<Label>(1); _poison = poison.GetChild<Label>(1);
        _buttons = new HFlowContainer(); AddChild(_buttons);
        AddEntry("技能", HeroSection.Skills); AddEntry("套装", HeroSection.Sets); AddEntry("奖励", HeroSection.Rewards);
        Resized += LayoutPanel; LayoutPanel();
    }
    // 使用快照中的英雄身份，不持有实体引用。
    public void Render(HeroSnapshot? hero)
    {
        _name.Text = hero?.DisplayName ?? "英雄";
        _subtitle.Text = hero?.Title ?? "";
        _level.Text = hero?.Level.ToString() ?? "";
        _buttons.Visible = _health.Visible = _mana.Visible = hero is not null;
        _portrait.Texture = hero is not null && !hero.Illustration.IsEmpty && ResourceLoader.Exists(hero.Illustration.ToString())
            ? GD.Load<Texture2D>(hero.Illustration.ToString()) : null;
        _portrait.Visible = _portrait.Texture is not null;
        LayoutPanel();
        if (hero is null) return;
        var health = hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth);
        _health.Render("生命", health, health);
        _mana.Render("魔法", hero.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana),
            hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana));
    }
    // 组件展示可调整肖像高度，横向英雄栏仍以自身可用高度约束。
    public void SetPortraitHeight(float height)
    {
        _portrait.CustomMinimumSize = Vector2.Zero;
        LayoutPanel();
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
            _armor.Text = hero.Armor.ToString(); _burn.Text = hero.Burn.ToString(); _poison.Text = hero.Poison.ToString();
            _buttons.Hide();
        }
        LayoutPanel();
    }
    private void AddEntry(string text, HeroSection section)
    {
        var key = section == HeroSection.Skills ? new StringName("skills")
            : section == HeroSection.Sets ? new StringName("sets") : new StringName("rewards");
        var button = new Button { Text = text, Icon = MatchTheme.Icon(key), TooltipText = text };
        button.AddThemeConstantOverride("icon_max_width", 16);
        button.AddThemeFontSizeOverride("font_size", 16);
        var style = MatchTheme.Surface(Colors.Transparent, new Color("716444"));
        style.ContentMarginLeft = style.ContentMarginRight = 4;
        button.AddThemeStyleboxOverride("normal", style);
        _buttons.AddThemeConstantOverride("h_separation", 6);
        _buttons.AddChild(button);
        button.Pressed += () => SectionRequested?.Invoke(section);
    }

    public override void _ExitTree() => Resized -= LayoutPanel;

    private void LayoutPanel()
    {
        if (_portrait is null) return;
        _identity.Size = Size;
        var portrait = Mathf.Clamp(Size.Y - 10, 64, 88);
        _portrait.CustomMinimumSize = Vector2.Zero;
        _portrait.Position = new Vector2(0, 2); _portrait.Size = Vector2.One * portrait;
        _level.Position = new Vector2(-4, portrait - 23); _level.Size = new Vector2(30, 28);
        var left = portrait + 20;
        var width = Mathf.Max(120, Size.X - left);
        _name.Position = new Vector2(left, 0); _name.Size = new Vector2(Mathf.Min(220, width), 36);
        _name.TooltipText = _subtitle.Text;
        _subtitle.Visible = width >= 310;
        _subtitle.Position = new Vector2(left + 230, 2); _subtitle.Size = new Vector2(Mathf.Max(1, width - 230), 26);
        _health.Position = new Vector2(left, 38); _health.Size = new Vector2(width, 22);
        _mana.Position = new Vector2(left, 64); _mana.Size = new Vector2(width, 18);
        _buttons.Position = _battle.Position = new Vector2(left, 86);
        _buttons.Size = _battle.Size = new Vector2(width, 32);
    }
}
