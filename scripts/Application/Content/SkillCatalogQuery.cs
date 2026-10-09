using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;

namespace Project_Star.Application.Content;

// 正式技能图鉴条目与全部支持等级的冻结快照（应用层）。
public sealed record SkillCatalogEntry(string FactionName, int InitialLevel, IReadOnlyList<SkillSnapshot> Levels)
{
    public SkillSnapshot InitialSkill => Levels.First(skill => skill.Level == InitialLevel);
}

// 技能图鉴查询只投影正式定义，不创建实例或推进对局（应用层）。
public static class SkillCatalogQuery
{
    // 新增正式技能自动进入图鉴，仅捕获定义支持的等级。
    public static IReadOnlyList<SkillCatalogEntry> Capture(IDefinitionCatalog catalog) => Array.AsReadOnly(
        catalog.Skills.Values.OrderBy(skill => skill.Attributes.Identity.FactionKey.ToString(), StringComparer.Ordinal)
            .ThenBy(skill => skill.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .Select(definition => new SkillCatalogEntry(FactionName(catalog, definition.Attributes.Identity.FactionKey),
                definition.InitialLevel, Array.AsReadOnly(Enumerable.Range(1, 5).Where(definition.SupportsLevel)
                    .Select(level => MatchDisplayQuery.FromSkill(definition, level)).ToArray()))).ToArray());

    private static string FactionName(IDefinitionCatalog catalog, StringName faction) => faction == GameFactions.Neutral
        ? "无阵营" : catalog.Heroes.Values.FirstOrDefault(hero => hero.Attributes.Identity.FactionKey == faction)
            ?.Attributes.Identity.DisplayName ?? faction.ToString();
}
