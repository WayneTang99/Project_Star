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

// 英雄身份、初始属性及选角和头像纹理的真实控件验证（表现层）。
internal static class HeroArtworkChecks
{
    internal static bool Check(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(HarlaHeroDefinition).Assembly);
        var panel = new PlayerHeroPanel(); owner.AddChild(panel);
        var selection = new HeroSelectionView { Size = new Vector2(1200, 600) }; owner.AddChild(selection);
        try
        {
            foreach (var (key, name, title) in new[] { ("harla", "哈尔拉", "机械师"), ("jiyun", "极云", "熊猫人"),
                ("mona", "莫娜", "小魔女"), ("paladin", "帕拉帝恩", "圣骑士"),
                ("robin", "罗宾", "冒险家"), ("valos", "瓦洛斯", "潜行者") })
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
                selection.Render("选角", [new KeyedAction(identity.Key, new UiAction(name))
                    { Illustration = hero.Illustration, Hero = HeroSelectionDetails.From(definition) }]);
                if (selection.GetNode<TextureRect>("CurrentPortrait").Texture?.ResourcePath != identity.Illustration.ToString()) return false;
                if (selection.GetNode<Label>("HeroName").Text != name || selection.GetNode<Label>("HeroTitle").Text != title
                    || selection.GetNode<GridContainer>("Stats").GetChild<Label>(1).Text != "100") return false;
            }
            var choices = registry.Heroes.Values.OrderBy(hero => hero.Attributes.Identity.Key.ToString())
                .Select(hero => new KeyedAction(hero.Attributes.Identity.Key, new UiAction(hero.Attributes.Identity.DisplayName))
                    { Illustration = hero.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(hero) }).ToArray();
            selection.Render("选择", choices);
            var entries = selection.GetNode<VBoxContainer>("Roster/Entries");
            if (entries.GetChildCount() != choices.Length) return false;
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            var initial = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            var submitted = 0;
            StringName submittedKey = new(""); long submittedRevision = 0;
            selection.Selected += (key, revision) => { submitted++; submittedKey = key; submittedRevision = revision; };
            var second = entries.GetChild<Button>(1);
            second.EmitSignal(Button.SignalName.Pressed); second.GrabFocus();
            var next = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            selection.Render("刷新", choices, 7);
            if (next == initial || submitted != 0 || selection.GetNode<TextureRect>("CurrentPortrait").Texture != next
                || entries.GetChild<Button>(1) != second || !second.HasFocus()) return false;
            selection._Input(new InputEventKey { Keycode = Key.Left, Pressed = true });
            if (selection.GetNode<TextureRect>("CurrentPortrait").Texture != initial) return false;
            selection._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            if (submitted != 1 || submittedKey != choices[0].Key || submittedRevision != 7) return false;
            var custom = choices[1] with { Hero = choices[1].Hero! with { MaxHealth = 321, Income = 9 } };
            selection.Render("属性副本", [custom]);
            if (selection.GetNode<GridContainer>("Stats").GetChild<Label>(1).Text != "321"
                || selection.GetNode<GridContainer>("Stats").GetChild<Label>(5).Text != "9") return false;
            second.EmitSignal(Button.SignalName.Pressed);
            if (submitted != 1) return false;
            var toggle = selection.GetNode<Button>("Attributes");
            toggle.ButtonPressed = false;
            if (selection.GetNode<GridContainer>("Stats").Visible) return false;
            toggle.ButtonPressed = true;
            selection.Render("不可选", [custom with { Action = new UiAction("不可选", Enabled: false, Reason: "验证禁用") }]);
            selection.GetNode<Button>("Choose").EmitSignal(Button.SignalName.Pressed);
            if (submitted != 1 || !selection.GetNode<Button>("Choose").Disabled) return false;
            selection.Render("空名单", []);
            if (entries.GetChildCount() != 0 || selection.GetNode<TextureRect>("CurrentPortrait").Texture is not null
                || selection.GetNode<GridContainer>("Stats").Visible) return false;
            panel.Render(null);
            return panel.GetNode<TextureRect>("Identity/Portrait").Texture is null
                && typeof(HeroIdentityAttributes).GetProperty(nameof(HeroIdentityAttributes.Illustration))!.SetMethod is null;
        }
        finally { owner.RemoveChild(panel); panel.Free(); owner.RemoveChild(selection); selection.Free(); }
    }
}
