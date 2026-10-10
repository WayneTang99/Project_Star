using System.Linq;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 双方共用的护甲与周期伤害徽章，跳字仅使用已结算事件。
public sealed partial class HeroBattleStatusView : HBoxContainer
{
    private PanelContainer _armor = null!;
    private PanelContainer _burn = null!;
    private PanelContainer _poison = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 5);
        _armor = Chip("Armor", "armor", "668799", "22343d", "8ccbff", "护甲抵消普通伤害");
        _burn = Chip("Burn", "burn", "a58052", "322a20", "ffba66", "灼伤：每0.6秒结算，先扣护甲，每次减少1");
        _poison = Chip("Poison", "poison", "68955d", "203325", "b5dc65", "中毒：每1秒结算，无视护甲，不衰减");
    }
    // 状态从有到无时立即隐藏；暂停保持同一Tick的反馈。
    public void Render(HeroBattleSnapshot? hero, BattlePlaybackViewModel? playback = null, SideId side = SideId.Player)
    {
        Visible = hero is not null;
        Update(_armor, hero?.Armor ?? 0, "", null);
        Update(_burn, hero?.Burn ?? 0, "0.6s", BattleStatus.Burn);
        Update(_poison, hero?.Poison ?? 0, "1s", BattleStatus.Poison);
        void Update(PanelContainer chip, int amount, string period, BattleStatus? status)
        {
            chip.Visible = amount > 0;
            chip.GetNode<Label>("Row/Amount").Text = amount.ToString();
            var cycle = chip.GetNode<Label>("Row/Period"); cycle.Text = period; cycle.Visible = period.Length > 0;
            chip.Modulate = Colors.White;
            var feedback = chip.GetNode<Label>("Overlay/Damage"); feedback.Hide();
            if (status is null || playback is null) return;
            var damage = playback.VisualEvents.OfType<DamageDealtEvent>().LastOrDefault(item => item.TargetSide == side && item.StatusOrigin == status);
            if (damage is null || playback.Tick - damage.Tick.Value > 8) return;
            var age = playback.Tick - damage.Tick.Value;
            var brightness = 1 + Mathf.Max(0, 1 - age / 3f) * .5f;
            chip.Modulate = new Color(brightness, brightness, brightness);
            feedback.Text = damage.ArmorAbsorbed > 0 ? $"-{damage.HealthDamage} / 甲-{damage.ArmorAbsorbed}" : $"-{damage.HealthDamage}";
            feedback.Position = new Vector2(2, -13 - age);
            feedback.Modulate = new Color(1, 1, 1, Mathf.Max(0, 1 - age / 9f)); feedback.Show();
        }
    }
    private PanelContainer Chip(string name, StringName icon, string border, string background, string ink, string tooltip)
    {
        var chip = new PanelContainer { Name = name, TooltipText = tooltip, MouseFilter = MouseFilterEnum.Pass, Visible = false };
        var style = MatchTheme.Surface(new Color(background), new Color(border));
        style.ContentMarginLeft = style.ContentMarginRight = 5; style.ContentMarginTop = style.ContentMarginBottom = 2;
        chip.AddThemeStyleboxOverride("panel", style); AddChild(chip);
        var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 3); chip.AddChild(row);
        row.AddChild(new TextureRect { Texture = MatchTheme.Icon(icon), CustomMinimumSize = new Vector2(12, 12),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            Material = AttributePalette.IconMaterial, Modulate = new Color(ink) });
        var value = new Label { Name = "Amount", MouseFilter = MouseFilterEnum.Ignore }; MatchTheme.Text(value, 14, new Color(ink), true, true); row.AddChild(value);
        var period = new Label { Name = "Period", MouseFilter = MouseFilterEnum.Ignore }; MatchTheme.Text(period, 9, new Color(ink), true); row.AddChild(period);
        var damage = new Label { Name = "Damage", Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        MatchTheme.Text(damage, 10, new Color(ink), true, true); damage.AddThemeConstantOverride("outline_size", 2);
        damage.AddThemeColorOverride("font_outline_color", new Color("071513"));
        // 普通Control承载跳字，避免跳字参与徽章最小宽高排版。
        var overlay = new Control { Name = "Overlay", MouseFilter = MouseFilterEnum.Ignore }; chip.AddChild(overlay); overlay.AddChild(damage);
        return chip;
    }
}
