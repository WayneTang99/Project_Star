using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 卡牌关联词条、机制及引用卡牌小框，仅从只读快照生成（表现层）。
public sealed partial class CardRelatedEffectsView : PanelContainer
{
    private float _maximumHeight = 520;
    private IReadOnlyList<CardCatalogEntry> _entries = Array.Empty<CardCatalogEntry>();
    private readonly ScrollContainer _scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly VBoxContainer _items = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private static readonly (string Name, string Text)[] Terms =
    [
        ("发动", "冷却结束后自动触发能力。"), ("回响", "响应指定事件触发，目标、条件和次数以卡牌效果为准。"),
        ("亡语", "此卡牌被摧毁时触发，具体效果以卡牌为准。"),
        ("光环", "来源有效期间持续生效，失效时撤销该来源的贡献。"), ("拾取", "获得此卡牌时结算，合并获得按合并后的等级结算。"),
        ("出售", "出售此卡牌自身时结算。"), ("交易", "响应获得或出售其他卡牌的交易事件。"),
        ("轮初", "每轮开始时结算，轮中获得不补发。"), ("开战", "战斗开始时触发一次。"),
        ("升级", "己方英雄每升一级触发，生效区域以卡牌效果为准。"), ("消耗", "使用后本场摧毁，下场战斗恢复。"),
        ("任务", "此卡牌独立累计条件进度，达标后解锁对应效果。"), ("历战", "战斗结算后生效，参战和胜负要求以卡牌效果为准。"), ("凯旋", "参战并获胜后结算。"),
        ("疾速", "持续期间冷却速度 ×2。"), ("迟缓", "持续期间冷却速度 ×0.5。"), ("禁锢", "持续期间冷却停止。"),
        ("灼伤", "每0.6秒造成当前灼伤值的伤害，优先扣护甲，随后灼伤减少1。"),
        ("中毒", "每1秒造成当前中毒值的伤害，无视护甲；本场不衰减，重复施加累加。"),
        ("飞行", "新受到的迟缓、禁锢时长减半，不影响已存在的状态。"),
        ("狂暴", "自身发动的伤害、中毒和灼伤施加量增加20%，向下取整。"),
        ("隐身", "阻挡直接攻击造成的生命和护甲伤害，不阻挡灼伤、中毒和日蚀。"),
        ("修复", "恢复本场被摧毁的卡牌，保留数值、能力和触发历史，从完整冷却重新开始；原位被占用或永久摧毁时不可修复。"),
        ("转变", "本场替换目标身份、属性和能力，保留位置及等级，清除原加成和状态，战后恢复原卡。"),
        ("摧毁", "使卡牌在本场失效；明确标注永久摧毁时才从卡池移除。"),
        ("召唤", "创建仅本场存在的临时卡牌，不进入卡池，不触发拾取或合并。"),
        ("充能", "立即减少当前剩余冷却，最低减至0；减至0时可以发动。"),
        ("冷却", "主动能力的完整冷却最低1秒；充能仍可把剩余冷却降到0。"),
        ("生命再生", "每1秒恢复生命，不能超过最大生命。"), ("魔法再生", "每1秒恢复魔法，不能超过最大魔法。"),
        ("日蚀", "进入后双方持续受到日蚀伤害，先扣护甲再扣生命，卡牌继续发动；全场只进入一次。"),
        ("多重", "主动发动后按次数追加发动，间隔0.2秒，不重复支付魔法。"),
    ];
    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color("3c342d"), new Color("ad967688")));
        AddChild(_scroll); _scroll.AddChild(_items); _items.AddThemeConstantOverride("separation", 9);
    }
    // 接收正式目录的只读投影，引用卡牌不创建实例或结算能力。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries) => _entries = entries;

    // 每项独立显示名称及效果，不添加分类标题。
    public void Render(CardSnapshot card, float width, float maximumHeight)
    {
        foreach (var child in _items.GetChildren()) { _items.RemoveChild(child); child.QueueFree(); }
        var cardRules = CardDisplayAdapter.Rules(card);
        var rules = string.Join("\n", cardRules.Concat(CardQuestViewModel.From(card, null).Select(q => q.Condition + q.Reward)));
        var references = Regex.Matches(rules, "【([^】]+)】").Select(match => match.Groups[1].Value).Distinct().ToArray();
        var text = Regex.Replace(rules, "【[^】]+】", "");
        foreach (var term in Terms.Where((term, index) => index < 13 ? cardRules.Any(rule => rule.StartsWith(term.Name, StringComparison.Ordinal)) || term.Name == "任务" && card.Quests.Count > 0 : text.Contains(term.Name, StringComparison.Ordinal))) AddBox(term.Name, term.Text, width, false);
        foreach (var name in references)
        {
            var entry = _entries.FirstOrDefault(entry => entry.InitialCard.DisplayName == name);
            var sameLevel = cardRules.Any(rule => rule.Contains("【" + name + "】", StringComparison.Ordinal) && rule.Contains("同级", StringComparison.Ordinal));
            var target = sameLevel ? entry?.Levels.FirstOrDefault(level => level.Card.Level == card.Level)?.Card ?? entry?.InitialCard : entry?.InitialCard;
            AddBox("【" + name + "】", target is null ? "未找到此卡牌的效果。" : $"{target.Level}级 · {string.Join(" · ", target.Tags.Select(Project_Star.Domain.Definitions.TagDisplayNames.Get))}\n" + string.Join("\n", CardDisplayAdapter.Rules(target)), width, true);
        }
        Visible = _items.GetChildCount() > 0;
        CustomMinimumSize = new Vector2(width, 0); Size = new Vector2(width, Math.Min(maximumHeight, 600));
        _maximumHeight = Math.Max(40, maximumHeight - 28);
        FitHeight();
        _scroll.ScrollVertical = 0;
    }
    public override void _Process(double delta) { if (Visible) FitHeight(); }
    private void FitHeight()
    {
        var height = Math.Min(_maximumHeight, Math.Max(30, _items.GetCombinedMinimumSize().Y));
        if (!Mathf.IsEqualApprox(_scroll.CustomMinimumSize.Y, height)) _scroll.CustomMinimumSize = new Vector2(0, height);
        Size = new Vector2(Size.X, GetCombinedMinimumSize().Y);
    }
    private void AddBox(string title, string text, float width, bool reference)
    {
        var panel = new PanelContainer(); var plate = MatchTheme.Surface(new Color("302923"), new Color("ad967644"));
        plate.BorderWidthLeft = 2; plate.BorderColor = new Color(reference ? "d5b979" : "83cec6"); panel.AddThemeStyleboxOverride("panel", plate); _items.AddChild(panel);
        var column = new VBoxContainer(); panel.AddChild(column);
        var heading = new Label { Text = title }; MatchTheme.Text(heading, 14, new Color(reference ? "e4cb91" : "83cec6"), bold: true); column.AddChild(heading);
        var body = new RichTextLabel { FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(Math.Max(1, width - 68), 0), MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeFontSizeOverride("normal_font_size", 14); column.AddChild(body); CardKeywordText.RenderRule(body, text);
    }
}
