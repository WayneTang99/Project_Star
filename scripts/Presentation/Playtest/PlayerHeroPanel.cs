using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly Dictionary<StringName, Vector3> PortraitCrops = new()
    {
        [new("hero.paladin")] = new(2.7f, .52f, .09f),
        [new("hero.mona")] = new(2.5f, .43f, .15f),
        [new("hero.harla")] = new(2.65f, .48f, 0),
        [new("hero.jiyun")] = new(2.5f, .53f, .03f),
        [new("hero.robin")] = new(2.5f, .58f, .01f),
        [new("hero.valos")] = new(2.8f, .42f, .02f),
    };
    public event Action<HeroSection>? SectionRequested;
    private Label _name = null!;
    private Label _subtitle = null!;
    private HeroBattleStatusView _battle = null!;
    private ResourceBar _health = null!;
    private ResourceBar _mana = null!;
    private Label _manaRegen = null!;
    private TextureRect _portrait = null!;
    private Control _identity = null!;
    private Label _level = null!;
    private HBoxContainer _skillIcons = null!;
    private string _skillSignature = "";
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
            CustomMinimumSize = Vector2.Zero, VerticalAlignment = VerticalAlignment.Center }; identity.AddChild(_name);
        MatchTheme.Text(_name, 15, spacing: 3);
        _subtitle = new Label { Name = "Subtitle", ClipText = true }; identity.AddChild(_subtitle);
        MatchTheme.Text(_subtitle, 10, MatchTheme.Muted, spacing: 1);
        _subtitle.AddThemeColorOverride("font_color", MatchTheme.Muted);
        _level = new Label { Name = "Level", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _level.AddThemeColorOverride("font_color", new Color("defaff"));
        MatchTheme.Text(_level, 20, new Color("defaff"), true);
        _level.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("325b67"), new Color("8fc5cc")));
        identity.AddChild(_level);
        _health = new ResourceBar("HealthBar", new Color("729c51")); AddChild(_health);
        _mana = new ResourceBar("ManaBar", new Color("438fa2")); AddChild(_mana);
        _health.SetDisplayHeight(24); _mana.SetDisplayHeight(24);
        _manaRegen = new Label { Name = "ManaRegen", Position = new Vector2(180, 0), Size = new Vector2(57, 24),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, TooltipText = "魔法再生：每秒恢复，不超过最大魔法" };
        MatchTheme.Text(_manaRegen, 15, AttributePalette.Find(GameAttributeKeys.ManaRegen), true, true); _mana.AddChild(_manaRegen);
        _portrait.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/PortraitClip.gdshader") };
        _battle = new HeroBattleStatusView { Name = "BattleResources", Visible = false, ShowArmor = false }; AddChild(_battle);
        _skillIcons = new HBoxContainer { Name = "SkillIcons" }; _skillIcons.AddThemeConstantOverride("separation", 7); AddChild(_skillIcons);
        Resized += LayoutPanel; LayoutPanel();
    }
    // 使用快照中的英雄身份，不持有实体引用。
    public void Render(HeroSnapshot? hero)
    {
        _name.Text = hero?.DisplayName ?? "英雄";
        _subtitle.Text = hero?.Title ?? "";
        _level.Text = hero?.Level.ToString() ?? "";
        _skillIcons.Visible = _health.Visible = _mana.Visible = hero is not null;
        _portrait.Texture = PortraitTexture(hero);
        _portrait.Visible = _portrait.Texture is not null;
        LayoutPanel();
        if (hero is null) return;
        var health = hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxHealth);
        _health.Render("生命", health, health);
        _mana.Render("魔法", hero.CombatValues.GetValueOrDefault(GameAttributeKeys.Mana),
            hero.CombatValues.GetValueOrDefault(GameAttributeKeys.MaxMana));
        _manaRegen.Text = "+" + hero.CombatValues.GetValueOrDefault(GameAttributeKeys.ManaRegen);
    }

    // 双方英雄小头像使用同一取景，怪物保留其正式插画。
    internal static Texture2D? PortraitTexture(HeroSnapshot? hero)
    {
        if (hero is null || hero.Illustration.IsEmpty || !ResourceLoader.Exists(hero.Illustration.ToString())) return null;
        var texture = GD.Load<Texture2D>(hero.Illustration.ToString());
        if (PortraitCrops.TryGetValue(hero.Key, out var crop))
        {
            // 对应HTML小头像的放大倍数与背景定位，仅裁显示区域，原画资源保持完整。
            var side = texture.GetWidth() / crop.X;
            return new AtlasTexture { Atlas = texture, Region = new Rect2(
                (texture.GetWidth() - side) * crop.Y, (texture.GetHeight() - side) * crop.Z, side, side) };
        }
        return texture;
    }
    // 组件展示可调整肖像高度，横向英雄栏仍以自身可用高度约束。
    public void SetPortraitHeight(float height)
    {
        _portrait.CustomMinimumSize = Vector2.Zero;
        LayoutPanel();
    }
    // 战斗HUD只显示播放中的英雄数值，播放结束恢复构筑入口。
    public void RenderBattle(HeroBattleSnapshot? hero, BattlePlaybackViewModel? playback = null)
    {
        _battle.Render(hero, playback);
        if (hero is not null)
        {
            _health.Show(); _mana.Show();
            _health.Render("生命", hero.Health, hero.MaxHealth);
            _mana.Render("魔法", hero.Mana, hero.MaxMana);
            _manaRegen.Text = "+" + hero.ManaRegen;
        }
        LayoutPanel();
    }

    // 实际获得的技能显示圆形插画，点击仍打开只读技能详情。
    public void RenderSkills(IReadOnlyList<SkillSnapshot> skills, int setCount)
    {
        var signature = string.Join("/", skills.Select(skill => $"{skill.Key}:{skill.Level}"));
        if (_skillSignature != signature)
        {
            _skillSignature = signature;
            foreach (var child in _skillIcons.GetChildren()) { _skillIcons.RemoveChild(child); child.QueueFree(); }
            foreach (var skill in skills.Take(skills.Count > 6 ? 5 : 6))
            {
                var button = new Button { CustomMinimumSize = new Vector2(28, 28), TooltipText = Project_Star.Presentation.CardFace.CardDisplayAdapter.SkillDetails(skill) };
                var style = MatchTheme.Surface(Colors.Transparent, new Color("c1aa76")); style.SetCornerRadiusAll(12);
                style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 0;
                button.AddThemeStyleboxOverride("normal", style); _skillIcons.AddChild(button);
                LevelPresentation.Frame(button, skill.Level, 12);
                if (!skill.Illustration.IsEmpty && ResourceLoader.Exists(skill.Illustration.ToString()))
                {
                    var art = new TextureRect { Texture = GD.Load<Texture2D>(skill.Illustration.ToString()), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
                        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Playtest/RoundIcon.gdshader") } };
                    button.AddChild(art); art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); art.OffsetLeft = art.OffsetTop = 1; art.OffsetRight = art.OffsetBottom = -1;
                }
                button.Pressed += () => SectionRequested?.Invoke(HeroSection.Skills);
            }
            if (skills.Count > 6)
            {
                var more = new Button { Name = "MoreSkills", Text = "展开", TooltipText = $"查看全部 {skills.Count} 项技能",
                    CustomMinimumSize = new Vector2(36, 28) };
                more.AddThemeFontSizeOverride("font_size", 10);
                more.Pressed += () => SectionRequested?.Invoke(HeroSection.Skills);
                _skillIcons.AddChild(more);
            }
        }
        LayoutPanel();
    }

    public override void _ExitTree() => Resized -= LayoutPanel;

    private void LayoutPanel()
    {
        if (_portrait is null) return;
        _identity.Size = Size;
        _portrait.CustomMinimumSize = Vector2.Zero;
        _portrait.Position = new Vector2(Size.X / 2 - 45, 42); _portrait.Size = new Vector2(90, 90);
        _name.Hide(); _subtitle.Hide(); _level.Hide();
        _health.Position = new Vector2(160, 14); _health.Size = new Vector2(244, 24);
        _mana.Position = new Vector2(796, 14); _mana.Size = new Vector2(244, 24);
        _skillIcons.Position = new Vector2(806, 54); _skillIcons.Size = new Vector2(224, 28);
        _battle.Position = new Vector2(796, -16); _battle.Size = new Vector2(244, 24);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_portrait is null || !_portrait.Visible) return;
        MatchTheme.DrawPortraitMount(this, _portrait.GetRect());
        DrawStyleBox(MatchTheme.Plate("hud-inset", 6), new Rect2(796, 50, 244, 36));
    }
}
