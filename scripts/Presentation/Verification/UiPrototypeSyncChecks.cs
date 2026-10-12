using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 当前原型同步的全卡描述、只读预览与真实浮层控件验证（表现层）。
internal static class UiPrototypeSyncChecks
{
    internal static bool Definitions()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var catalog = CardCatalogQuery.Capture(registry);
        var query = new CardPreviewQuery(registry);
        foreach (var level in catalog.SelectMany(entry => entry.Levels))
        {
            var text = string.Join("\n", CardDisplayAdapter.Rules(level.Card)) + "\n" + CardDisplayAdapter.CurrentDetails(level.Card)
                + "\n" + CardDisplayAdapter.SetDetails(query.Describe(level.Card, null));
            if (text.Contains("基础 ") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+/\d+/\d+|MaxHealth|HealthRegen|MaxMana|ManaRegen|Small型|Medium型|Large型")) return false;
        }
        return AttributePalette.Find(AttributePalette.Health) == AttributePalette.Find(GameAttributeKeys.HealthRegen)
            && AttributePalette.Find(GameAttributeKeys.HealthRegen) == AttributePalette.Find(GameAttributeKeys.HealingBonus)
            && AttributePalette.Find(GameAttributeKeys.Mana) == AttributePalette.Find(GameAttributeKeys.ManaRegen)
            && AttributePalette.Find(AttributePalette.Slow) == AttributePalette.Find(AttributePalette.Immobilize)
            && AttributePalette.Find(AttributePalette.TextKey("充能")) == AttributePalette.Find(AttributePalette.Haste);
    }

    internal static bool Preview()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var query = new CardPreviewQuery(registry);
        var entries = CardCatalogQuery.Capture(registry);
        var boar = entries.Single(entry => entry.InitialCard.DisplayName == "野猪").Levels.Single(level => level.Card.Level == 2).Card;
        var grown = boar with { Value = 15, OwnValues = new Dictionary<StringName, int>(boar.CurrentValues) { [GameAttributeKeys.AttackDamage] = 37 },
            CurrentValues = new Dictionary<StringName, int>(boar.CurrentValues) { [GameAttributeKeys.AttackDamage] = 37 } };
        var result = query.Preview(grown, 4, new StringName?[grown.GemSockets.Count]);
        if (result.Level != 4 || result.CurrentValues[GameAttributeKeys.AttackDamage] != 107
            || grown.Level != 2 || grown.Value != 15 || grown.CurrentValues[GameAttributeKeys.AttackDamage] != 37) return false;
        var gemKeys = new StringName?[grown.GemSockets.Count]; gemKeys[0] = query.Gems[0].Key;
        var gem = query.Preview(grown, 4, gemKeys);
        if (gem.GemSockets[0]?.Key != query.Gems[0].Key || grown.GemSockets.Any(item => item is not null)
            || gem.Abilities.Where(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active)
                .Sum(ability => ability.Effects.Count) <= result.Abilities.Where(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active).Sum(ability => ability.Effects.Count)) return false;
        var crossbow = entries.Single(entry => entry.InitialCard.DisplayName == "机关重弩").InitialCard;
        return CardDisplayAdapter.Rules(crossbow).Count(line => line.StartsWith("多重")) == 1
            && new CardDisplayAdapter().Build(crossbow).Effects.Any(effect => effect.Kind == CardFaceEffectKind.Multicast);
    }

    internal static bool Scene(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var query = new CardPreviewQuery(registry);
        var sample = CardCatalogQuery.Capture(registry).Single(entry => entry.InitialCard.DisplayName == "机关重弩").InitialCard;
        var preview = new CardPreviewView { Size = new Vector2(1600, 900) }; owner.AddChild(preview); preview.SetQuery(query);
        var label = new RichTextLabel(); owner.AddChild(label);
        var origin = new Button(); owner.AddChild(origin);
        try
        {
            CardKeywordText.RenderRule(label, "己方大型机械光属性卡牌冷却 -2秒，随机小型人类获得疾速1秒。");
            if (label.GetParsedText() != "己方[大型][机械][光属性]卡牌冷却 -2秒，随机[小型][人类]获得疾速1秒。") return false;
            origin.GrabFocus(); preview.Open(sample, null, null);
            var levels = preview.GetNode<Control>("Panel").FindChildren("Level*", "Button", true, false).OfType<Button>().ToArray();
            var four = levels.Single(button => button.Text == "4级"); four.EmitSignal(Button.SignalName.Pressed);
            if (!preview.Visible || preview.DisplayedCard?.Level != 4 || preview.DisplayedCard.Value != 24) return false;
            var option = preview.FindChildren("Socket0", "OptionButton", true, false).Cast<OptionButton>().Single();
            option.Select(1); option.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            if (preview.DisplayedCard?.GemSockets[0] is null || sample.GemSockets[0] is not null) return false;
            preview.FindChildren("Reset", "Button", true, false).Cast<Button>().Single().EmitSignal(Button.SignalName.Pressed);
            if (preview.DisplayedCard?.Level != sample.Level || preview.DisplayedCard.GemSockets[0] is not null) return false;
            preview._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            return !preview.Visible && owner.GetViewport().GuiGetFocusOwner() == origin;
        }
        finally { owner.RemoveChild(origin); origin.Free(); owner.RemoveChild(label); label.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var previousHdr = owner.GetViewport().UseHdr2D; owner.GetViewport().UseHdr2D = false;
        var preview = new CardPreviewView { Size = owner.GetViewportRect().Size, Theme = MatchTheme.Create() }; owner.AddChild(preview);
        preview.SetQuery(new CardPreviewQuery(registry));
        var entries = CardCatalogQuery.Capture(registry);
        var sample = entries.Single(entry => entry.InitialCard.DisplayName == "机关重弩").InitialCard;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output/ui-sync"));
            preview.Open(sample, null, null); await Save("native-preview");
            var shield = entries.Single(entry => entry.InitialCard.DisplayName == "魔能盾").InitialCard;
            preview.Open(shield, null, null); await Save("native-arcane-shield");
            preview.Open(shield with { CurrentValues = new Dictionary<StringName, int>(shield.CurrentValues)
                { [GameAttributeKeys.Armor] = 7 }, OwnValues = new Dictionary<StringName, int>(shield.OwnValues)
                { [GameAttributeKeys.Armor] = 7 } }, null, null);
            await Save("native-arcane-shield-bonus");
            preview.Open(entries.Single(entry => entry.InitialCard.DisplayName == "随军牧师").InitialCard, null, null);
            await Save("native-mana-cost");
            preview.Open(entries.First(entry => entry.InitialCard.SetKey is not null).InitialCard, null, null);
            await Save("native-set-preview");
            preview.Close();
            var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
            try
            {
                var potion = entries.Single(entry => entry.InitialCard.DisplayName == "小型生命药水").InitialCard;
                details.ShowCard(potion, new Vector2(420, 140), owner.GetViewportRect().Size);
                for (var frame = 0; frame < 12; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await Save("native-potion-details");
            }
            finally { owner.RemoveChild(details); details.Free(); }
        }
        finally { owner.RemoveChild(preview); preview.Free(); owner.GetViewport().UseHdr2D = previousHdr; }

        async Task Save(string name)
        {
            for (var frame = 0; frame < 4; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            image.SavePng(ProjectSettings.GlobalizePath($"res://output/ui-sync/{name}.png"));
        }
    }
}
