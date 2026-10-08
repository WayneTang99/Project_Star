using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Content;

// 单个正式卡牌等级的基础展示快照（应用层），不持有对局或可变定义。
public sealed record CardCatalogLevel(CardSnapshot Card, int InitialValue);

// 图鉴条目只保存归属展示名、初始等级和全部支持等级（应用层）。
public sealed record CardCatalogEntry(string FactionName, int InitialLevel, IReadOnlyList<CardCatalogLevel> Levels)
{
    public CardSnapshot InitialCard => Levels.First(level => level.Card.Level == InitialLevel).Card;
}

// 从正式内容目录生成图鉴；查询不创建实体、不执行拾取效果或推进随机（应用层）。
public static class CardCatalogQuery
{
    // 捕获全部已注册卡牌，后续新增正式定义自动进入图鉴。
    public static IReadOnlyList<CardCatalogEntry> Capture(IDefinitionCatalog catalog) => Array.AsReadOnly(
        catalog.Cards.Values.OrderBy(card => card.Attributes.Identity.FactionKey.ToString(), StringComparer.Ordinal)
            .ThenBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .Select(definition => new CardCatalogEntry(FactionName(catalog, definition.Attributes.Identity.FactionKey),
                definition.InitialLevel, Array.AsReadOnly(Enumerable.Range(1, 5).Where(definition.SupportsLevel)
                    .Select(level =>
                    {
                        var offer = ShopOffer.Create(definition, level);
                        return new CardCatalogLevel(MatchDisplayQuery.FromOffer(offer), offer.Price);
                    }).ToArray()))).ToArray());

    private static string FactionName(IDefinitionCatalog catalog, StringName faction) => faction == GameFactions.Neutral
        ? "无阵营" : catalog.Heroes.Values.FirstOrDefault(hero => hero.Attributes.Identity.FactionKey == faction)
            ?.Attributes.Identity.DisplayName ?? faction.ToString();
}
