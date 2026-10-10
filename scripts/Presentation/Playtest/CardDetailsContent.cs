using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 悬停与固定详情共用结构化内容，全部信息来自冻结卡牌快照。
public sealed partial class CardDetailsContent : VBoxContainer
{
    private HFlowContainer _tags = null!;
    private Label _name = null!;
    private PanelContainer _cooldown = null!;
    private Label _seconds = null!;
    private VBoxContainer _paragraphs = null!;
    private ScrollContainer _scroll = null!;
    private Label _identity = null!;
    private Label _value = null!;
    private Button _extra = null!;
    private RichTextLabel _attributes = null!;
    private float _maximumBodyHeight = 240;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 0);
        var header = new HBoxContainer { Name = "Header" }; header.AddThemeConstantOverride("separation", 10); AddChild(header);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 4); header.AddChild(titles);
        _tags = new HFlowContainer { Name = "Tags" }; _tags.AddThemeConstantOverride("h_separation", 6); titles.AddChild(_tags);
        _name = new Label { Name = "CardName", AutowrapMode = TextServer.AutowrapMode.WordSmart }; MatchTheme.Text(_name, 20, new Color("f0deb5"), bold: true, spacing: 2); titles.AddChild(_name);
        titles.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        _cooldown = new PanelContainer { Name = "Cooldown", CustomMinimumSize = new Vector2(49, 49), SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        var circle = MatchTheme.Plate("cooldown-circle", 0);
        circle.ContentMarginLeft = circle.ContentMarginRight = 1; circle.ContentMarginTop = 9; circle.ContentMarginBottom = 0;
        _cooldown.AddThemeStyleboxOverride("panel", circle); header.AddChild(_cooldown);
        var time = new VBoxContainer(); time.AddThemeConstantOverride("separation", 1); _cooldown.AddChild(time);
        _seconds = new Label { Name = "Seconds", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_seconds, 20, new Color("efddb7"), true, true); time.AddChild(_seconds);
        var unit = new Label { Text = "秒", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(unit, 8, new Color("a7b19c")); time.AddChild(unit);
        Space(15); AddChild(Line()); Space(13);
        _scroll = new ScrollContainer { Name = "BodyScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _paragraphs = new VBoxContainer { Name = "Paragraphs", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _paragraphs.AddThemeConstantOverride("separation", 8); _scroll.AddChild(_paragraphs);
        Space(14); AddChild(Line()); Space(12);
        var footer = new HBoxContainer { Name = "Footer" }; footer.AddThemeConstantOverride("separation", 4); AddChild(footer);
        _identity = new Label { Name = "Identity", SizeFlagsHorizontal = SizeFlags.ExpandFill }; MatchTheme.Text(_identity, 10, new Color("acb59f")); footer.AddChild(_identity);
        var caption = new Label { Text = "价值" }; MatchTheme.Text(caption, 10, AttributePalette.Find(GameAttributeKeys.Value)); footer.AddChild(caption);
        footer.AddChild(new TextureRect { Texture = MatchTheme.Icon("coin"), CustomMinimumSize = new Vector2(10, 10), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = AttributePalette.Find(GameAttributeKeys.Value)!.Value, Material = AttributePalette.IconMaterial });
        _value = new Label { Name = "Value" }; MatchTheme.Text(_value, 14, AttributePalette.Find(GameAttributeKeys.Value), true, true); footer.AddChild(_value);
        _extra = new Button { Name = "InstanceToggle", Text = "宝石 · 当前属性  ▾", Alignment = HorizontalAlignment.Left };
        _extra.AddThemeFontSizeOverride("font_size", 10); _extra.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); AddChild(_extra);
        _attributes = new RichTextLabel { Name = "InstanceDetails", CustomMinimumSize = new Vector2(0, 160), Visible = false };
        _attributes.AddThemeFontSizeOverride("normal_font_size", 12); AddChild(_attributes); _extra.Pressed += ToggleAttributes;
    }

    // 规则主体与实例属性分区，保留宝石、任务和能力公式的完整语义。
    public void Render(CardSnapshot card, float width, float maximumBodyHeight, CardBattleSnapshot? battle = null, bool preserveExpansion = false)
    {
        _maximumBodyHeight = maximumBodyHeight;
        _name.Text = card.DisplayName;
        foreach (var child in _tags.GetChildren()) { _tags.RemoveChild(child); child.QueueFree(); }
        var tags = new[] { GameTags.FromSize(card.Size) }.Concat(card.Tags.Where(tag => tag != GameTags.FromSize(card.Size)))
            .Select(TagDisplayNames.Get).Concat(card.ElementKeys.Select(element => element == GameElements.General ? "无属性" : CardDisplayAdapter.ElementName(element)));
        foreach (var tag in tags)
        {
            var badge = new Label { Text = tag }; MatchTheme.Text(badge, 10, new Color("9aaa91"), spacing: 1);
            var plate = MatchTheme.Surface(new Color("1c2922"), new Color("727255")); plate.ContentMarginLeft = plate.ContentMarginRight = 5; plate.ContentMarginTop = plate.ContentMarginBottom = 3;
            plate.SetCornerRadiusAll(0);
            badge.AddThemeStyleboxOverride("normal", plate); _tags.AddChild(badge);
        }
        var ticks = card.CurrentValues.GetValueOrDefault(GameAttributeKeys.CooldownTicks);
        _cooldown.Visible = ticks > 0 && card.Abilities.Any(ability => ability.Activation == AbilityActivation.Active);
        _seconds.Text = $"{ticks / 10m * card.CooldownMultiplier:0.0}";
        foreach (var child in _paragraphs.GetChildren()) { _paragraphs.RemoveChild(child); child.QueueFree(); }
        var details = CardDisplayAdapter.Details(card, includeQuestProgress: false).Split('\n');
        var count = card.DescriptionEntries.Count > 0 ? card.DescriptionEntries.Count : card.Abilities.Sum(ability => 1 + ability.Effects.Count);
        var offset = 3 + card.GemSockets.Count;
        var height = 0f;
        var rules = card.DescriptionEntries.Count > 0
            ? card.DescriptionEntries.Where(entry => entry.KeywordKey != CardKeywords.Quest)
                .Select(entry => $"{CardKeywords.DisplayName(entry.KeywordKey)}　{entry.Text}")
            : details.Skip(offset).Take(count);
        foreach (var paragraph in rules)
        {
            AddParagraph("Rule", paragraph, width);
            height += Math.Max(22, MatchTheme.Font().GetMultilineStringSize(paragraph, width: width, fontSize: 12).Y * 1.3f) + 8;
        }
        if (battle is not null)
        {
            var status = string.Join(" · ", new[]
            {
                battle.IsFlying ? "飞行" : "",
                battle.IsBerserk ? "狂暴" : "",
                battle.Immobilize > 0 ? $"禁锢 {battle.Immobilize / 10m:0.0}s" : "",
                battle.Haste > 0 ? $"疾速 {battle.Haste / 10m:0.0}s" : "",
                battle.Slow > 0 ? $"迟缓 {battle.Slow / 10m:0.0}s" : "",
                battle.Destroyed ? "已摧毁" : "",
            }.Where(value => value.Length > 0));
            if (status.Length > 0)
            {
                AddParagraph("BattleState", $"战斗状态：{status}", width, new Color("e6c784"));
                height += Math.Max(22, MatchTheme.Font().GetMultilineStringSize($"战斗状态：{status}", width: width, fontSize: 12).Y * 1.3f) + 8;
            }
        }
        var quests = CardQuestViewModel.From(card, battle);
        if (quests.Count > 0) RenderQuests(quests, width);
        if (!preserveExpansion) _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(maximumBodyHeight, Mathf.Max(22, height - 8)));
        _identity.Text = $"{card.Level}级 · {string.Join(" / ", card.ElementKeys.Select(element => element == GameElements.General ? "无属性" : CardDisplayAdapter.ElementName(element) + "属性"))}"; _value.Text = card.Value.ToString();
        CardKeywordText.Render(_attributes, string.Join("\n", details.Skip(2).Take(1 + card.GemSockets.Count).Concat(details.Skip(offset + count))));
        _attributes.CustomMinimumSize = new Vector2(0, Mathf.Min(160, maximumBodyHeight / 2));
        if (!preserveExpansion) { _attributes.Hide(); _scroll.ScrollVertical = 0; _attributes.GetVScrollBar().Value = 0; }
    }

    public override void _ExitTree() => _extra.Pressed -= ToggleAttributes;
    public override void _Process(double delta)
    {
        var available = _maximumBodyHeight - (_attributes.Visible ? _attributes.CustomMinimumSize.Y : 0);
        var height = Mathf.Min(available, Mathf.Max(22, _paragraphs.GetCombinedMinimumSize().Y));
        if (!Mathf.IsEqualApprox(_scroll.CustomMinimumSize.Y, height)) _scroll.CustomMinimumSize = new Vector2(0, height);
    }
    private void ToggleAttributes() => _attributes.Visible = !_attributes.Visible;
    private void RenderQuests(IReadOnlyList<CardQuestViewModel> quests, float width)
    {
        var section = new VBoxContainer { Name = "Quests", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", 8); _paragraphs.AddChild(section);
        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore });
        section.AddChild(Line());
        var heading = new HBoxContainer(); section.AddChild(heading);
        var title = new Label { Text = "成长任务", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        MatchTheme.Text(title, 15, new Color("efd9a8"), spacing: 2); heading.AddChild(title);
        var total = new Label { Text = $"{quests.Count}项 · 已解锁 {quests.Count(quest => quest.Unlocked)}项" };
        MatchTheme.Text(total, 11, new Color("adbaaa")); heading.AddChild(total);
        foreach (var quest in quests)
        {
            var panel = new PanelContainer { Name = quest.Key.ToString().Replace('.', '_'), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var plate = new StyleBoxFlat { BgColor = new Color(quest.Unlocked ? "20382d" : "293025"),
                BorderColor = new Color(quest.Unlocked ? "75bba166" : "8b754c66"),
                ContentMarginLeft = 13, ContentMarginRight = 13, ContentMarginTop = 12, ContentMarginBottom = 12 };
            plate.SetBorderWidthAll(1); plate.BorderWidthLeft = 3; panel.AddThemeStyleboxOverride("panel", plate); section.AddChild(panel);
            var column = new VBoxContainer { Name = "Column" }; column.AddThemeConstantOverride("separation", 9); panel.AddChild(column);
            var row = new HBoxContainer { Name = "Heading" }; column.AddChild(row);
            var condition = new Label { Name = "Condition", Text = quest.Condition, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            MatchTheme.Text(condition, 13, new Color("eee1c0")); row.AddChild(condition);
            var state = new Label { Name = "State", Text = quest.Unlocked ? "✓ 已解锁" : "进行中" };
            MatchTheme.Text(state, 10, new Color(quest.Unlocked ? "a4dfbc" : "dac78f")); row.AddChild(state);
            var meter = new HBoxContainer { Name = "Meter" }; meter.AddThemeConstantOverride("separation", 12); column.AddChild(meter);
            var bar = new ProgressBar { Name = "Progress", MaxValue = quest.RequiredCount, Value = Math.Clamp(quest.Progress, 0, quest.RequiredCount),
                ShowPercentage = false, CustomMinimumSize = new Vector2(0, 7), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            var background = new StyleBoxFlat { BgColor = new Color("0f1b16"), BorderColor = new Color("9c845b66") };
            background.SetBorderWidthAll(1); bar.AddThemeStyleboxOverride("background", background);
            bar.AddThemeStyleboxOverride("fill", new StyleBoxTexture { Texture = new GradientTexture2D { Width = 64, Height = 1,
                FillFrom = Vector2.Zero, FillTo = new Vector2(1, 0), Gradient = new Gradient { Colors =
                    new[] { new Color(quest.Unlocked ? "467e67" : "886a38"), new Color(quest.Unlocked ? "9cd8ba" : "e6c77e") } } } });
            meter.AddChild(bar);
            var count = new Label { Name = "Count", Text = $"{quest.Progress} / {quest.RequiredCount}", HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(72, 0) };
            MatchTheme.Text(count, 17, new Color("ead4a5"), serif: true, bold: true); meter.AddChild(count);
            var reward = new RichTextLabel { Name = "Reward", FitContent = true, ScrollActive = false, Size = new Vector2(Mathf.Max(1, width - 26), 0),
                MouseFilter = MouseFilterEnum.Ignore };
            reward.AddThemeFontSizeOverride("normal_font_size", 12); reward.AddThemeColorOverride("default_color", new Color("d5dac7")); column.AddChild(reward);
            CardKeywordText.RenderRule(reward, $"解锁奖励：{quest.Reward}");
        }
    }
    private RichTextLabel AddParagraph(string name, string paragraph, float width, Color? color = null)
    {
        var text = new RichTextLabel { Name = name, FitContent = true, ScrollActive = false, Size = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeFontSizeOverride("normal_font_size", 12); text.AddThemeColorOverride("default_color", color ?? new Color("d9dbc3"));
        var leading = Mathf.Max(0, 22.2f - MatchTheme.Font().GetHeight(12));
        text.AddThemeConstantOverride("line_separation", Mathf.RoundToInt(leading));
        text.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginTop = leading / 2, ContentMarginBottom = leading / 2 });
        _paragraphs.AddChild(text); CardKeywordText.RenderRule(text, paragraph);
        return text;
    }
    private void Space(float height) => AddChild(new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore });
    private static HSeparator Line()
    {
        var line = new HSeparator(); line.AddThemeConstantOverride("separation", 1);
        line.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color("8e775447"), ContentMarginTop = 1 }); return line;
    }
}
