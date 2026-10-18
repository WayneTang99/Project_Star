using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Content;

// 局外目录仅投影正式定义，不创建实体、访问句柄或随机状态。
public sealed record WorldCatalogEntry(StringName Key, StringName Category, string Name, StringName Illustration,
    string Summary, IReadOnlyList<CardSnapshot> Cards, IReadOnlyList<SkillSnapshot> Skills);

public static class WorldCatalogQuery
{
    public static readonly StringName Heroes = new("heroes"), Monsters = new("monsters"),
        Encounters = new("encounters"), Mentors = new("mentors"), Sets = new("sets");

    public static IReadOnlyList<WorldCatalogEntry> Capture(IDefinitionCatalog catalog)
    {
        var entries = new List<WorldCatalogEntry>();
        var preview = new CardPreviewQuery(catalog);
        CardSnapshot CaptureCard(CardDefinition card, int level) => preview.Describe(MatchDisplayQuery.FromOffer(ShopOffer.Create(card, level)), null);
        foreach (var hero in catalog.Heroes.Values)
        {
            var id = hero.Attributes.Identity;
            entries.Add(new(id.Key, Heroes, id.DisplayName, id.Illustration,
                $"{id.Title}\n最大生命 {hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth)}\n最大魔法 {hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana)}\n每轮收入 {hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Income)}",
                Array.Empty<CardSnapshot>(), Array.Empty<SkillSnapshot>()));
        }
        foreach (var monster in catalog.Monsters.Values)
        {
            var id = monster.Attributes.Identity;
            entries.Add(new(id.Key, Monsters, id.DisplayName, id.Illustration,
                $"{monster.Level}级 · 第{monster.MinimumRound}—{monster.MaximumRound}轮\n生命 {monster.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth)} · 护甲 {monster.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor)}",
                Array.AsReadOnly(monster.Cards.OrderBy(card => card.BoardStart).Select(card =>
                    CaptureCard(catalog.Cards[card.CardKey], card.Level)).ToArray()),
                Array.AsReadOnly(monster.Skills.Select(skill => MatchDisplayQuery.FromSkill(catalog.Skills[skill.SkillKey], skill.Level)).ToArray())));
        }
        foreach (var encounter in catalog.Encounters.Values)
        {
            var id = encounter.Attributes.Identity;
            var choices = encounter is ChoiceEncounterDefinition choice
                ? "\n" + string.Join("\n", choice.Options.Select(option => option.DisplayName)) : "";
            entries.Add(new(id.Key, Encounters, id.DisplayName, id.Illustration,
                $"{id.Summary}\n第{encounter.MinimumRound}—{encounter.MaximumRound}轮{choices}",
                Array.Empty<CardSnapshot>(), Array.Empty<SkillSnapshot>()));
        }
        foreach (var mentor in catalog.Mentors.Values)
        {
            var id = mentor.Attributes.Identity;
            entries.Add(new(id.Key, Mentors, id.DisplayName, id.Illustration, id.Summary,
                Array.Empty<CardSnapshot>(), Array.AsReadOnly(catalog.Skills.Values
                    .Where(skill => catalog.Heroes.Values.Any(hero => mentor.CanOfferSkill(skill, hero.Attributes.Identity)))
                    .Select(skill => MatchDisplayQuery.FromSkill(skill, skill.InitialLevel)).ToArray())));
        }
        foreach (var set in catalog.Sets.Values)
        {
            var id = set.Attributes.Identity;
            entries.Add(new(id.Key, Sets, id.DisplayName, new StringName(""), "",
                Array.AsReadOnly(catalog.Cards.Values.Where(card => card.Attributes.Identity.SetKey == id.Key)
                    .Select(card => CaptureCard(card, card.InitialLevel)).ToArray()),
                Array.Empty<SkillSnapshot>()));
        }
        return Array.AsReadOnly(entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray());
    }
}
