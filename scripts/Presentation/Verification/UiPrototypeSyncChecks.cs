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
            var rules = CardDisplayAdapter.Rules(level.Card);
            var described = query.Describe(level.Card, null);
            if (described.Set?.Key == new StringName("set.apocalypse")
                && !CardDisplayAdapter.SetDetails(described).Contains("敌方英雄在日蚀时间内，受到的日蚀伤害 ×2")) return false;
            if (rules.Count(rule => rule.StartsWith("交易　", StringComparison.Ordinal))
                != level.Card.SaleAttributeBonuses.Count + level.Card.AcquisitionAttributeBonuses.Count
                || rules.Count(rule => rule.StartsWith("出售　", StringComparison.Ordinal)) != (level.Card.OnSellReward is null ? 0 : 1)) return false;
            var text = string.Join("\n", CardDisplayAdapter.Rules(level.Card)) + "\n" + CardDisplayAdapter.CurrentDetails(level.Card)
                + "\n" + CardDisplayAdapter.SetDetails(query.Describe(level.Card, null));
            if (text.Contains("基础 ") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+/\d+/\d+|MaxHealth|HealthRegen|MaxMana|ManaRegen|Small型|Medium型|Large型")) return false;
        }
        // 改变冻结结果只改变卡面，动态机制的详情公式保持稳定。
        foreach (var name in new[] { "野猪", "碧玉蟾", "冒险者大剑" })
        {
            var card = catalog.Single(entry => entry.InitialCard.DisplayName == name).InitialCard;
            var first = card with { EffectAmounts = new int?[] { 17 } };
            var second = card with { EffectAmounts = new int?[] { 999 } };
            if (!CardDisplayAdapter.Rules(first).SequenceEqual(CardDisplayAdapter.Rules(second))
                || CardDisplayAdapter.FaceEffects(first).First().Value != "17"
                || CardDisplayAdapter.FaceEffects(second).First().Value != "999") return false;
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

    // 全卡等级与宝石预览必须保留护甲基值和实例加成，再由战斗层计算光环。
    internal static bool PreviewArmor()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var query = new CardPreviewQuery(registry);
        foreach (var entry in CardCatalogQuery.Capture(registry))
        foreach (var sourceLevel in entry.Levels)
        foreach (var targetLevel in entry.Levels)
        {
            var source = sourceLevel.Card;
            var target = targetLevel.Card;
            if (!target.Abilities.SelectMany(a => a.Effects).OfType<Project_Star.Domain.Combat.GainSourceHeroArmorFromAttributeEffectDefinition>().Any()) continue;
            var originalArmor = source.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor);
            var own = new Dictionary<StringName, int>(source.CurrentValues);
            own[GameAttributeKeys.Armor] = own.GetValueOrDefault(GameAttributeKeys.Armor) + 7;
            var grown = source with { OwnValues = own, CurrentValues = own };
            var sockets = new StringName?[source.GemSockets.Count];
            // 同等级仍切换宝石，确保进入实际战斗预览计算。
            sockets[0] = query.Gems[0].Key;
            var preview = query.Preview(grown, target.Level, sockets);
            var expected = target.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor) + 7;
            var setup = new Project_Star.Domain.Combat.CardBattleSetup(EntityId.New(), 0,
                target.CurrentValues.GetValueOrDefault(GameAttributeKeys.AttackDamage),
                target.CurrentValues.GetValueOrDefault(GameAttributeKeys.CooldownTicks), target.Abilities,
                UseLegacyAttack: false, Tags: new TagSet(target.Tags), OccupiedSlots: (int)target.Size,
                ElementKeys: target.ElementKeys, ArmorAmount: expected)
                { Level = target.Level, CombatValues = target.CurrentValues };
            var state = new Project_Star.Domain.Combat.CombatSimulator().Preview(new(
                new(new(EntityId.New(), 200, 0), [setup]), new(new(EntityId.New(), 200, 0), []), 1,
                new BattleTick(1)), false).Cards.Single();
            if (preview.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor)
                != state.Values.GetValueOrDefault(GameAttributeKeys.Armor))
            {
                GD.Print($"护甲预览丢失：{source.DisplayName} {source.Level}→{target.Level}，实际{preview.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor)}，预期{state.Values.GetValueOrDefault(GameAttributeKeys.Armor)}");
                return false;
            }
            if (source.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor) != originalArmor) return false;
        }
        return true;
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
            var details = preview.FindChild("Details", true, false) as CardDetailsContent;
            if (details is null || !details.GetNode<Control>("HeaderMargin").Visible) return false;
            var labels = details.FindChild("Tags", true, false)!.GetChildren().OfType<Label>().Select(item => item.Text).ToArray();
            if (sample.Tags.Any(tag => !labels.Contains(TagDisplayNames.Get(tag)))
                || details.GetNode<Control>("HeaderMargin/Header/Cooldown").CustomMinimumSize != new Vector2(68, 68)) return false;
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

    // 真实战場投影与等级预览保留冷却；100%缩减显示1秒，被动卡不显示圆徽。
    internal static bool CooldownDetails(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var factory = new Project_Star.Application.Factories.EntityFactory();
        var board = new Project_Star.Application.Board.BoardService(new Project_Star.Domain.Match.BoardPlacementSolver(), registry.Sets);
        var session = new Project_Star.Domain.Match.MatchSession(42, 100);
        session.Player.SelectHero(factory.CreateHero(registry.Heroes[new StringName("hero.paladin")]));
        var economy = new Project_Star.Application.Economy.CardEconomyService(factory);
        var instance = economy.AcquireCard(session, registry.Cards[new StringName("card.frankent")], 3,
            Project_Star.Application.Economy.CardAcquisitionSource.Reward).Value!.Card;
        board.PlaceCard(session, instance.Id, Project_Star.Domain.Match.BoardZone.Battlefield, 0);
        var capture = MatchDisplayQuery.Capture(session, registry.Sets);
        var live = capture.Cards.Single();
        if (live.CurrentCooldownSeconds != 6m) return false;
        var query = new CardPreviewQuery(registry);
        var minimum = query.Preview(live with { CooldownMultiplier = 0m }, 4,
            live.GemSockets.Select(gem => (StringName?)gem?.Key).ToArray(), capture);
        if (minimum.CurrentCooldownSeconds != 1m) return false;
        var content = new CardDetailsContent(); owner.AddChild(content);
        try
        {
            foreach (var card in CardCatalogQuery.Capture(registry).SelectMany(entry => entry.Levels).Select(level => level.Card)
                .Append(live).Append(query.Preview(live, 4, live.GemSockets.Select(gem => (StringName?)gem?.Key).ToArray(), capture))
                .Append(minimum))
            {
                content.Render(card, 396, 420);
                var active = card.Abilities.Any(ability => ability.Activation == Project_Star.Domain.Combat.AbilityActivation.Active);
                var badge = content.GetNode<Control>("HeaderMargin/Header/Cooldown");
                var seconds = badge.FindChild("Seconds", true, false) as Label;
                if (badge.Visible != active || seconds?.Text != $"{CardDisplayAdapter.CooldownSeconds(card):0.0}") return false;
            }
            return true;
        }
        finally { owner.RemoveChild(content); content.Free(); }
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
            preview.Open(entries.Single(entry => entry.InitialCard.DisplayName == "野猪").InitialCard, null, null);
            await Save("native-formula-boar");
            preview.Open(entries.Single(entry => entry.InitialCard.DisplayName == "碧玉蟾").InitialCard, null, null);
            await Save("native-formula-toad");
            preview.Open(entries.Single(entry => entry.InitialCard.DisplayName == "辰光之门").InitialCard, null, null);
            await Save("native-passive-gate");
            foreach (var (name, file) in new[] { ("飞天扫帚", "echo-state"), ("传动齿轮", "echo-direction"), ("神圣狮鹫", "echo-reaction"), ("小天使", "echo-enemy-tags"), ("枪矛手", "echo-both-sides") })
            {
                preview.Open(entries.Single(entry => entry.InitialCard.DisplayName == name).InitialCard, null, null);
                await Save(file);
            }

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
