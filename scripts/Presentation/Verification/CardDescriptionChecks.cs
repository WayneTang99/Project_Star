using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 全卡各级文案审查与原生引用样式验收（表现层验证）。
internal static class CardDescriptionChecks
{
    internal static bool AllLevels()
    {
        var registry = DefinitionRegistry.Scan(typeof(CardDescriptionChecks).Assembly);
        var catalog = CardCatalogQuery.Capture(registry);
        var query = new CardPreviewQuery(registry);
        var report = new StringBuilder(); var failures = new List<string>(); var count = 0;
        foreach (var entry in catalog)
        foreach (var level in entry.Levels)
        {
            count++;
            var card = level.Card;
            var rules = string.Join("\n", CardDisplayAdapter.Rules(card));
            var text = rules + "\n" + CardDisplayAdapter.CurrentDetails(card) + "\n" + CardDisplayAdapter.Details(card, false) + "\n"
                + CardDisplayAdapter.SetDetails(query.Describe(card, null)) + "\n"
                + string.Join("\n", card.QuestDefinitions.Select(quest => CardDisplayAdapter.QuestReward(card, quest)));
            report.AppendLine($"## {card.DisplayName} {card.Level}级\n{text}\n");
            if (Regex.IsMatch(text, "[A-Za-z:：]") || text.Contains("己方英雄己方英雄")
                || text.Contains("己方英雄　己方英雄")) failures.Add($"{card.DisplayName} {card.Level}级\n{text}");
            foreach (var effect in card.Abilities.SelectMany(ability => ability.Effects))
            {
                IEnumerable<CardDefinition> references = effect switch
                {
                    SummonAdjacentCardEffectDefinition value => new[] { value.Card },
                    SummonIntoAllEmptySlotsEffectDefinition value => new[] { value.Card },
                    SacrificeSourceAndNeighborsToSummonEffectDefinition value => new[] { value.Card },
                    SummonRandomAdjacentCardEffectDefinition value => value.Cards,
                    TransformRandomEnemyCardEffectDefinition value => new[] { value.Replacement },
                    TransformSourceIntoRandomCardsEffectDefinition value => value.Replacements,
                    GrantSourceLevelCardEffectDefinition value => new[] { value.Card },
                    _ => Array.Empty<CardDefinition>(),
                };
                if (references.Any(reference => !rules.Contains($"【{reference.Attributes.Identity.DisplayName}】")))
                    failures.Add($"卡牌引用遗漏：{card.DisplayName} {card.Level}级");
            }
        }
        var root = ProjectSettings.GlobalizePath("res://output/card-description-copy"); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "audit.md"), $"卡牌{catalog.Count}张，等级配置{count}组。\n\n" + report);
        File.WriteAllText(Path.Combine(root, "failures.txt"), string.Join("\n\n", failures));
        GD.Print($"全卡文案审查：{catalog.Count}张卡牌，{count}组等级，{failures.Count}项问题。");
        return failures.Count == 0;
    }

    internal static bool TargetsAndReferences()
    {
        string Rules(CardDefinition definition) => string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition))));
        var crusader = Rules(new OrderCrusaderCardDefinition());
        var armor = Rules(new ArcaneShieldCardDefinition());
        var summon = Rules(new TemplarKnightCardDefinition());
        var transform = Rules(new PolymorphWandCardDefinition());
        return crusader.Contains("此卡牌攻击") && !Regex.IsMatch(crusader, "[A-Za-z]")
            && Regex.Matches(armor, "己方英雄").Count == 1 && !armor.Contains('：')
            && summon.Contains("【骑佩短剑】") && transform.Contains("【咩咩羊】")
            && !summon.Contains("己方英雄");
    }

    internal static bool ReferenceRendering()
    {
        var label = new RichTextLabel();
        try
        {
            const string text = "召唤【寒冰短剑】，转变为【咩咩羊】，己方光属性卡牌。";
            CardKeywordText.RenderRule(label, text);
            return label.GetParsedText() == "召唤【寒冰短剑】，转变为【咩咩羊】，己方[光属性]卡牌。";
        }
        finally { label.Free(); }
    }

    internal static async Task Capture(Node owner)
    {
        GD.Print($"全卡文案：{AllLevels()}，目标与引用：{TargetsAndReferences()}，卡名完整渲染：{ReferenceRendering()}");
        var registry = DefinitionRegistry.Scan(typeof(CardDescriptionChecks).Assembly);
        var query = new CardPreviewQuery(registry);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview); preview.SetQuery(query);
        try
        {
            CardDefinition[] cards = [new OrderCrusaderCardDefinition(), new ArcaneShieldCardDefinition(),
                new TemplarKnightCardDefinition(), new PolymorphWandCardDefinition(), new BaaSheepCardDefinition()];
            string[] names = ["crusader", "shield", "summon", "transform", "sheep"];
            for (var index = 0; index < cards.Length; index++)
            {
                preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(cards[index])), null, null);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = owner.GetViewport().GetTexture().GetImage();
                if (image.SavePng($"res://output/card-description-copy/{names[index]}.png") != Error.Ok)
                    throw new InvalidOperationException("卡牌描述截图失败。");
                preview.Close();
            }
        }
        finally { owner.RemoveChild(preview); preview.Free(); }
    }
}
