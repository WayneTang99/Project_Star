using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 正式 CSV 与运行定义的结构化字段一致性验证（表现层验证模块）。
internal static class ContentDataChecks
{
    // 核对卡牌身份、分类、初始等级与完整 key 集合。
    internal static bool Cards() => CardsMatch(Load("CardDataTable.csv"), Catalog());

    // 词条与说明逐行核对；每张卡的顺序从1连续递增，不解析正文。
    internal static bool Descriptions() => DescriptionsMatch(Load("CardDescriptionTable.csv"), Catalog());

    internal static IReadOnlyDictionary<StringName, IReadOnlyList<CardDescriptionEntry>> DescriptionRows()
    {
        var table = Load("CardDescriptionTable.csv");
        return table.Rows.GroupBy(row => new StringName(table.Value(row, "CardKey")))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CardDescriptionEntry>)Array.AsReadOnly(
                group.OrderBy(row => table.Number(row, "顺序")).Select(row => new CardDescriptionEntry(
                    new StringName(table.Value(row, "词条")), table.Value(row, "说明"))).ToArray()));
    }

    private static bool DescriptionsMatch(CsvTable table, IDefinitionCatalog catalog)
    {
        var groups = table.Rows.GroupBy(row => new StringName(table.Value(row, "CardKey"))).ToArray();
        if (!groups.Select(group => group.Key).ToHashSet().SetEquals(catalog.Cards.Keys)) return false;
        foreach (var group in groups)
        {
            var rows = group.OrderBy(row => table.Number(row, "顺序")).ToArray();
            var expected = catalog.Cards[group.Key].Attributes.Identity.DescriptionEntries;
            if (rows.Length != expected.Count) return false;
            for (var index = 0; index < rows.Length; index++)
                if (table.Number(rows[index], "顺序") != index + 1
                    || table.Value(rows[index], "词条") != expected[index].KeywordKey.ToString()
                    || table.Value(rows[index], "说明") != expected[index].Text) return false;
        }
        return true;
    }

    internal static bool RejectsDescriptionDrift()
    {
        var catalog = Catalog();
        var changed = Load("CardDescriptionTable.csv");
        changed.Rows[0][Array.IndexOf(changed.Header, "词条")] = "keyword.unknown";
        var missing = Load("CardDescriptionTable.csv");
        missing.Rows.RemoveAt(0);
        var duplicateOrder = Load("CardDescriptionTable.csv");
        duplicateOrder.Rows[0][Array.IndexOf(duplicateOrder.Header, "顺序")] = "2";
        var empty = Load("CardDescriptionTable.csv");
        empty.Rows[0][Array.IndexOf(empty.Header, "说明")] = "";
        return !DescriptionsMatch(changed, catalog) && !DescriptionsMatch(missing, catalog)
            && !DescriptionsMatch(duplicateOrder, catalog) && !DescriptionsMatch(empty, catalog);
    }

    // 输入集合变更不影响只读身份；同类词条可重复，正文标点不影响分段。
    internal static bool DescriptionEntryBoundaries()
    {
        var entries = new List<CardDescriptionEntry>
        {
            new(CardKeywords.Activate, "原样保留；发动：这仍是正文。"),
            new(CardKeywords.Activate, "第二条发动。"), new(CardKeywords.Echo, "回响说明。"),
            new(CardKeywords.Aura, "光环说明。"), new(CardKeywords.Quest, "任务说明。"),
            new(CardKeywords.Pickup, "拾取说明。"), new(CardKeywords.Sell, "出售说明。"),
        };
        var identity = new CardIdentityAttributes(new StringName("card.description_fixture"), "描述夹具",
            GameFactions.Neutral, CardSize.Small, [GameElements.General], descriptionEntries: entries);
        entries.Clear();
        if (identity.DescriptionEntries.Count != 7
            || typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.DescriptionEntries))!.CanWrite
            || typeof(CardDescriptionEntry).GetProperty(nameof(CardDescriptionEntry.KeywordKey))!.CanWrite
            || typeof(CardDescriptionEntry).GetProperty(nameof(CardDescriptionEntry.Text))!.CanWrite) return false;
        try { ((IList<CardDescriptionEntry>)identity.DescriptionEntries).Clear(); return false; }
        catch (NotSupportedException) { }
        try { _ = new CardDescriptionEntry(new StringName("keyword.unknown"), "说明"); return false; }
        catch (ArgumentException) { }
        try { _ = new CardDescriptionEntry(CardKeywords.Activate, " "); return false; }
        catch (ArgumentException) { }
        return identity.DescriptionEntries.Select(entry => CardKeywords.DisplayName(entry.KeywordKey))
            .SequenceEqual(new[] { "发动", "发动", "回响", "光环", "任务", "拾取", "出售" });
    }

    private static bool CardsMatch(CsvTable table, IDefinitionCatalog catalog)
    {
        if (table.Rows.Count != catalog.Cards.Count
            || table.Rows.Select(row => table.Value(row, "CardKey")).Distinct().Count() != table.Rows.Count) return false;
        foreach (var row in table.Rows)
        {
            if (!catalog.Cards.TryGetValue(new StringName(table.Value(row, "CardKey")), out var definition)) return false;
            var identity = definition.Attributes.Identity;
            var tags = Keys(table.Value(row, "额外标签")).ToHashSet();
            if (table.Value(row, "名称") != identity.DisplayName
                || table.Value(row, "归属") != identity.FactionKey.ToString()
                || table.Value(row, "尺寸") != identity.Size.ToString()
                || !GameElements.Normalize(Keys(table.Value(row, "元素"))).SequenceEqual(identity.ElementKeys)
                || !tags.SetEquals(definition.Tags.Where(tag => tag != GameTags.FromSize(identity.Size)))
                || table.Number(row, "初始等级") != definition.InitialLevel
                || table.Value(row, "插画") != identity.Illustration.ToString()) return false;
        }
        return true;
    }

    // 核对英雄定义及工厂创建后的初始等级，包含省略等级时的现行默认值。
    internal static bool Heroes()
    {
        var table = Load("HeroDataTable.csv");
        var catalog = Catalog();
        if (!table.HasUniqueKeys("HeroKey", catalog.Heroes.Keys)) return false;
        (string Column, StringName Attribute)[] combat = [
            ("最大生命", GameAttributeKeys.MaxHealth), ("护甲", GameAttributeKeys.Armor),
            ("最大魔法", GameAttributeKeys.MaxMana), ("初始魔法", GameAttributeKeys.Mana),
            ("魔法再生", GameAttributeKeys.ManaRegen), ("生命再生", GameAttributeKeys.HealthRegen),
        ];
        foreach (var row in table.Rows)
        {
            var definition = catalog.Heroes[new StringName(table.Value(row, "HeroKey"))];
            var attributes = definition.Attributes;
            var instance = new EntityFactory().CreateHero(definition);
            if (table.Value(row, "名称") != attributes.Identity.DisplayName
                || table.Value(row, "称号") != attributes.Identity.Title
                || table.Value(row, "归属") != attributes.Identity.FactionKey.ToString()
                || table.Number(row, "初始等级") != instance.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level)
                || table.Number(row, "收入") != attributes.Persistent.GetFinalValue(GameAttributeKeys.Income)
                || combat.Any(field => table.Number(row, field.Column)
                    != attributes.BaseCombat.GetFinalValue(field.Attribute))) return false;
        }
        return true;
    }

    // 技能支持等级来自定义，触发与效果文案由各技能行为检查保护。
    internal static bool Skills()
    {
        var table = Load("SkillDataTable.csv");
        var catalog = Catalog();
        if (!table.HasUniqueKeys("SkillKey", catalog.Skills.Keys)) return false;
        foreach (var row in table.Rows)
        {
            var definition = catalog.Skills[new StringName(table.Value(row, "SkillKey"))];
            if (table.Value(row, "名称") != definition.Attributes.Identity.DisplayName
                || table.Value(row, "归属") != definition.Attributes.Identity.FactionKey.ToString()
                || table.Number(row, "初始等级") != definition.InitialLevel
                || !Range(table.Value(row, "支持等级")).SequenceEqual(
                    Enumerable.Range(1, 5).Where(definition.SupportsLevel))) return false;
        }
        return true;
    }

    // 遭遇和怪物投影的排程字段、选项归属与名称保持一致。
    internal static bool Encounters()
    {
        var table = Load("EncounterDataTable.csv");
        var catalog = Catalog();
        var encounters = table.Rows.Where(row => table.Value(row, "记录类型") == "遭遇").ToArray();
        var options = table.Rows.Where(row => table.Value(row, "记录类型") == "选项").ToArray();
        if (encounters.Length + options.Length != table.Rows.Count
            || encounters.Length != catalog.Encounters.Count + catalog.Monsters.Count
            || encounters.Select(row => table.Value(row, "EncounterKey")).Distinct().Count() != encounters.Length)
            return false;
        foreach (var row in encounters)
        {
            var key = new StringName(table.Value(row, "EncounterKey"));
            if (catalog.Monsters.TryGetValue(key, out var monster))
            {
                if (table.Value(row, "名称") != monster.Attributes.Identity.DisplayName
                    || table.Value(row, "类型") != "怪物战" || table.Value(row, "轮次") != "1～99"
                    || table.Number(row, "基础权重") != 1 || table.Number(row, "等级") != monster.Level) return false;
                continue;
            }
            if (!catalog.Encounters.TryGetValue(key, out var definition)) return false;
            var kind = definition.Kind switch { EncounterKind.Shop => "商店", EncounterKind.Pvp => "PvP", _ => "可选项" };
            var level = definition switch { ShopEncounterDefinition shop => shop.Level.ToString(CultureInfo.InvariantCulture),
                ChoiceEncounterDefinition choice => choice.Level.ToString(CultureInfo.InvariantCulture), _ => "—" };
            if (table.Value(row, "名称") != definition.Attributes.Identity.DisplayName
                || table.Value(row, "类型") != kind || table.Value(row, "等级") != level
                || !Range(table.Value(row, "轮次")).SequenceEqual(
                    Enumerable.Range(definition.MinimumRound, definition.MaximumRound - definition.MinimumRound + 1))
                || table.Number(row, "基础权重") != definition.BaseWeight) return false;
        }
        var expected = catalog.Encounters.Values.OfType<ChoiceEncounterDefinition>()
            .SelectMany(encounter => encounter.Options.Select(option =>
                (Encounter: encounter.Attributes.Identity.Key, Option: option.Key, Name: option.DisplayName)))
            .ToDictionary(item => (item.Encounter, item.Option), item => item.Name);
        var seen = new HashSet<(StringName, StringName)>();
        foreach (var row in options)
        {
            var key = (new StringName(table.Value(row, "EncounterKey")), new StringName(table.Value(row, "选项Key")));
            if (!seen.Add(key) || !expected.TryGetValue(key, out var name) || table.Value(row, "选项名") != name) return false;
        }
        return seen.Count == expected.Count;
    }

    // 验证校验确实拒绝文案漂移、缺失记录及重复 key。
    internal static bool RejectsDrift()
    {
        var catalog = Catalog();
        var changed = Load("CardDataTable.csv");
        changed.Rows[0][Array.IndexOf(changed.Header, "名称")] += "漂移";
        var missing = Load("CardDataTable.csv");
        missing.Rows.RemoveAt(0);
        var duplicate = Load("CardDataTable.csv");
        duplicate.Rows[0] = duplicate.Rows[1];
        return !CardsMatch(changed, catalog) && !CardsMatch(missing, catalog) && !CardsMatch(duplicate, catalog);
    }

    private static IDefinitionCatalog Catalog() => DefinitionRegistry.Scan(typeof(ContentDataChecks).Assembly);

    private static IEnumerable<StringName> Keys(string value) => value == "—" ? []
        : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(key => new StringName(key));

    private static IEnumerable<int> Range(string value)
    {
        var bounds = value.Split('～');
        var first = int.Parse(bounds[0], CultureInfo.InvariantCulture);
        var last = bounds.Length == 1 ? first : int.Parse(bounds[1], CultureInfo.InvariantCulture);
        return Enumerable.Range(first, last - first + 1);
    }

    private static CsvTable Load(string filename)
    {
        using var csv = FileAccess.Open("res://docs/design/" + filename, FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"无法读取正式数据表 {filename}");
        var header = csv.GetCsvLine();
        if (header.Distinct().Count() != header.Length) throw new InvalidOperationException($"表头重复：{filename}");
        var rows = new List<string[]>();
        while (!csv.EofReached())
        {
            var row = csv.GetCsvLine();
            if (row.Length == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
            if (row.Length != header.Length) throw new InvalidOperationException($"数据列数不一致：{filename}");
            rows.Add(row);
        }
        return new CsvTable(header, rows);
    }

    // 仅供正式数据校验使用的 CSV 夹具，不进入运行时内容加载。
    private sealed record CsvTable(string[] Header, List<string[]> Rows)
    {
        internal string Value(string[] row, string column)
        {
            var index = Array.IndexOf(Header, column);
            return index < 0 ? throw new InvalidOperationException($"缺少数据列：{column}") : row[index];
        }

        internal int Number(string[] row, string column) => int.Parse(Value(row, column), CultureInfo.InvariantCulture);

        internal bool HasUniqueKeys(string column, IEnumerable<StringName> expected)
        {
            var keys = Rows.Select(row => new StringName(Value(row, column))).ToArray();
            return keys.Length == keys.Distinct().Count() && keys.ToHashSet().SetEquals(expected);
        }
    }
}
