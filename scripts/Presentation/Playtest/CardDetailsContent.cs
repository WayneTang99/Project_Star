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
        _name = new Label { Name = "CardName", AutowrapMode = TextServer.AutowrapMode.WordSmart }; MatchTheme.Text(_name, 20, new Color("f0deb5"), spacing: 2); titles.AddChild(_name);
        _cooldown = new PanelContainer { Name = "Cooldown", CustomMinimumSize = new Vector2(49, 49), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var circle = MatchTheme.Surface(new Color("20261d"), new Color("bda173")); circle.SetCornerRadiusAll(25);
        circle.ContentMarginLeft = circle.ContentMarginRight = 0; circle.ContentMarginTop = circle.ContentMarginBottom = 3;
        _cooldown.AddThemeStyleboxOverride("panel", circle); header.AddChild(_cooldown);
        var time = new VBoxContainer(); time.AddThemeConstantOverride("separation", 0); _cooldown.AddChild(time);
        _seconds = new Label { Name = "Seconds", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(_seconds, 20, new Color("efddb7"), true, true); time.AddChild(_seconds);
        var unit = new Label { Text = "秒", HorizontalAlignment = HorizontalAlignment.Center }; MatchTheme.Text(unit, 8, new Color("a7b19c")); time.AddChild(unit);
        Space(15); AddChild(Line()); Space(13);
        _scroll = new ScrollContainer { Name = "BodyScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; AddChild(_scroll);
        _paragraphs = new VBoxContainer { Name = "Paragraphs", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _paragraphs.AddThemeConstantOverride("separation", 8); _scroll.AddChild(_paragraphs);
        Space(14); AddChild(Line()); Space(12);
        var footer = new HBoxContainer { Name = "Footer" }; AddChild(footer);
        _identity = new Label { Name = "Identity", SizeFlagsHorizontal = SizeFlags.ExpandFill }; MatchTheme.Text(_identity, 10, new Color("acb59f")); footer.AddChild(_identity);
        var caption = new Label { Text = "价值" }; MatchTheme.Text(caption, 10, new Color("acb59f")); footer.AddChild(caption);
        footer.AddChild(new TextureRect { Texture = MatchTheme.Icon("coin"), CustomMinimumSize = new Vector2(10, 10), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        _value = new Label { Name = "Value" }; MatchTheme.Text(_value, 14, new Color("e2c68d"), true, true); footer.AddChild(_value);
        _extra = new Button { Name = "InstanceToggle", Text = "宝石 · 当前属性 · 任务  ▾", Alignment = HorizontalAlignment.Left };
        _extra.AddThemeFontSizeOverride("font_size", 10); _extra.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); AddChild(_extra);
        _attributes = new RichTextLabel { Name = "InstanceDetails", CustomMinimumSize = new Vector2(0, 160), Visible = false };
        _attributes.AddThemeFontSizeOverride("normal_font_size", 12); AddChild(_attributes); _extra.Pressed += ToggleAttributes;
    }

    // 规则主体与实例属性分区，保留宝石、任务和能力公式的完整语义。
    public void Render(CardSnapshot card, float width, float maximumBodyHeight, CardBattleSnapshot? battle = null)
    {
        _maximumBodyHeight = maximumBodyHeight;
        _name.Text = card.DisplayName;
        foreach (var child in _tags.GetChildren()) { _tags.RemoveChild(child); child.QueueFree(); }
        foreach (var tag in card.Tags)
        {
            var badge = new Label { Text = TagDisplayNames.Get(tag) }; MatchTheme.Text(badge, 10, new Color("9aaa91"), spacing: 1);
            var plate = MatchTheme.Surface(new Color("1c2922"), new Color("727255")); plate.ContentMarginLeft = plate.ContentMarginRight = 5; plate.ContentMarginTop = plate.ContentMarginBottom = 3;
            badge.AddThemeStyleboxOverride("normal", plate); _tags.AddChild(badge);
        }
        var ticks = card.CurrentValues.GetValueOrDefault(GameAttributeKeys.CooldownTicks);
        _cooldown.Visible = ticks > 0 && card.Abilities.Any(ability => ability.Activation == AbilityActivation.Active);
        _seconds.Text = $"{ticks / 10m * card.CooldownMultiplier:0.0}";
        foreach (var child in _paragraphs.GetChildren()) { _paragraphs.RemoveChild(child); child.QueueFree(); }
        var details = CardDisplayAdapter.Details(card).Split('\n');
        var count = card.DescriptionEntries.Count > 0 ? card.DescriptionEntries.Count : card.Abilities.Sum(ability => 1 + ability.Effects.Count);
        var offset = 3 + card.GemSockets.Count;
        var height = 0f;
        foreach (var paragraph in details.Skip(offset).Take(count))
        {
            var text = new RichTextLabel { Name = "Rule", FitContent = true, ScrollActive = false, Size = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeFontSizeOverride("normal_font_size", 12); text.AddThemeColorOverride("default_color", new Color("d9dbc3")); text.AddThemeConstantOverride("line_separation", 6);
            _paragraphs.AddChild(text); CardKeywordText.Render(text, paragraph);
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
                var text = new RichTextLabel { Name = "BattleState", FitContent = true, ScrollActive = false, Size = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };
                text.AddThemeFontSizeOverride("normal_font_size", 12); text.AddThemeColorOverride("default_color", new Color("e6c784"));
                _paragraphs.AddChild(text); CardKeywordText.Render(text, $"战斗状态：{status}");
                height += Math.Max(22, MatchTheme.Font().GetMultilineStringSize($"战斗状态：{status}", width: width, fontSize: 12).Y * 1.3f) + 8;
            }
            foreach (var quest in battle.Quests.Where(quest => quest.RequiredCount > 0))
            {
                var questText = new RichTextLabel { Name = "BattleQuest", FitContent = true, ScrollActive = false, Size = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };
                questText.AddThemeFontSizeOverride("normal_font_size", 12); questText.AddThemeColorOverride("default_color", new Color("d9dbc3"));
                _paragraphs.AddChild(questText); CardKeywordText.Render(questText, $"任务 {quest.Progress}/{quest.RequiredCount}{(quest.Unlocked ? " · 已解锁" : "")}");
                height += 30;
            }
        }
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(maximumBodyHeight, Mathf.Max(22, height - 8)));
        _identity.Text = $"{card.Level}级 · {string.Join(" / ", card.ElementKeys.Select(CardDisplayAdapter.ElementName))}"; _value.Text = card.Value.ToString();
        CardKeywordText.Render(_attributes, string.Join("\n", details.Skip(2).Take(1 + card.GemSockets.Count).Concat(details.Skip(offset + count)))); _attributes.Hide();
    }

    public override void _ExitTree() => _extra.Pressed -= ToggleAttributes;
    public override void _Process(double delta)
    {
        var height = Mathf.Min(_maximumBodyHeight, Mathf.Max(22, _paragraphs.GetCombinedMinimumSize().Y));
        if (!Mathf.IsEqualApprox(_scroll.CustomMinimumSize.Y, height)) _scroll.CustomMinimumSize = new Vector2(0, height);
    }
    private void ToggleAttributes() => _attributes.Visible = !_attributes.Visible;
    private void Space(float height) => AddChild(new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore });
    private static HSeparator Line()
    {
        var line = new HSeparator(); line.AddThemeConstantOverride("separation", 1);
        line.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color("8e775447"), ContentMarginTop = 1 }); return line;
    }
}
