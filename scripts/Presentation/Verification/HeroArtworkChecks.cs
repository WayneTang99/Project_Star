using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 四位英雄身份、初始属性及选角和头像纹理的真实控件验证（表现层）。
internal static class HeroArtworkChecks
{
    internal static bool Check(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(HarlaHeroDefinition).Assembly);
        var panel = new PlayerHeroPanel(); owner.AddChild(panel);
        var selection = new HeroSelectionView(); owner.AddChild(selection);
        try
        {
            foreach (var (key, name, title) in new[] { ("harla", "哈尔拉", "机械师"), ("jiyun", "极云", "熊猫人"),
                ("mona", "莫娜", "小魔女"), ("paladin", "帕拉帝恩", "圣骑士") })
            {
                var definition = registry.Heroes[new StringName("hero." + key)];
                var identity = definition.Attributes.Identity;
                var session = new CreateMatchService(new EntityFactory()).Create(42, 100, definition);
                var snapshot = MatchSnapshot.From(session);
                var hero = snapshot.Hero!;
                if (identity.DisplayName != name || identity.Title != title || identity.FactionKey != new StringName(key)
                    || identity.Illustration.IsEmpty || hero.Illustration != identity.Illustration || hero.Level != 1 || snapshot.Income != 5
                    || hero.CombatValues[GameAttributeKeys.MaxHealth] != 100 || hero.CombatValues[GameAttributeKeys.MaxMana] != 100
                    || hero.CombatValues[GameAttributeKeys.Mana] != 0 || hero.CombatValues[GameAttributeKeys.Armor] != 0
                    || hero.CombatValues[GameAttributeKeys.ManaRegen] != 10 || hero.CombatValues[GameAttributeKeys.HealthRegen] != 0) return false;
                panel.Render(hero);
                var portrait = panel.GetNode<TextureRect>("Identity/Portrait");
                if (!portrait.Visible || portrait.Texture?.ResourcePath != identity.Illustration.ToString()
                    || portrait.Texture.GetWidth() < 512 || portrait.Texture.GetHeight() < 512) return false;
                selection.Render("选角", [new KeyedAction(identity.Key, new UiAction(name)) { Illustration = hero.Illustration }]);
                if (selection.GetNode<TextureRect>("CurrentPortrait").Texture?.ResourcePath != identity.Illustration.ToString()) return false;
            }
            var choices = registry.Heroes.Values.OrderBy(hero => hero.Attributes.Identity.Key.ToString())
                .Select(hero => new KeyedAction(hero.Attributes.Identity.Key, new UiAction(hero.Attributes.Identity.DisplayName))
                    { Illustration = hero.Attributes.Identity.Illustration }).ToArray();
            selection.Render("选择", choices);
            var initial = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            var submitted = 0;
            selection.Selected += (_, _) => submitted++;
            selection.GetNode<Button>("Next").EmitSignal(Button.SignalName.Pressed);
            var next = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            selection.Render("刷新", choices);
            if (next == initial || submitted != 0 || selection.GetNode<TextureRect>("CurrentPortrait").Texture != next) return false;
            selection.GetNode<Button>("Previous").EmitSignal(Button.SignalName.Pressed);
            if (selection.GetNode<TextureRect>("CurrentPortrait").Texture != initial) return false;
            selection.GetNode<Button>("Choose").EmitSignal(Button.SignalName.Pressed);
            if (submitted != 1) return false;
            panel.Render(null);
            return panel.GetNode<TextureRect>("Identity/Portrait").Texture is null
                && typeof(HeroIdentityAttributes).GetProperty(nameof(HeroIdentityAttributes.Illustration))!.SetMethod is null;
        }
        finally { owner.RemoveChild(panel); panel.Free(); owner.RemoveChild(selection); selection.Free(); }
    }
}
