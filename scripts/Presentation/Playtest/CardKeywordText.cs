using System.Text.RegularExpressions;
using System;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 表现层为卡牌文本中的效果关键词着色，保留原始描述及字面文本。
internal static class CardKeywordText
{
    private static readonly Regex RuleTokens = new("最大生命值|最大生命|生命值|生命|普通伤害|治疗加成|疾速时长加成|魔法回复|魔法|再生|价值|金币|金钱|发动|回响|任务|光环|拾取|开战|战后|凯旋|被动|消耗|出售|攻击|伤害|护甲|治疗|中毒|灼伤|疾速|迟缓|飞行|狂暴|充能|禁锢|Poison|Burn|Haste|Slow|Immobilize|(?<number>[0-9]+(?:\\.[0-9]+)?%?)");

    // 用富文本颜色栈显示纯文本，避免将内容中的括号解释为BBCode。
    public static void Render(RichTextLabel label, string text)
    {
        label.Clear();
        Append(label, text);
    }

    // 详情规则突出关键词与数字，仍按字面文本显示，不解析内容中的BBCode。
    public static void RenderRule(RichTextLabel label, string text)
    {
        label.Clear();
        Append(label, text, true);
    }

    // 卡名在卡面隐藏后由详情标题承载，正文仍保留全部冻结数值和规则。
    public static void RenderDetails(RichTextLabel label, string text)
    {
        label.Clear();
        var end = text.IndexOf('\n');
        label.PushFontSize(24); label.PushColor(MatchTheme.Gold);
        label.AddText(end < 0 ? text : text[..end]);
        label.Pop(); label.Pop();
        if (end >= 0) { label.AddText("\n\n"); Append(label, text[(end + 1)..]); }
    }

    private static void Append(RichTextLabel label, string text, bool emphasizeNumbers = false)
    {
        var offset = 0;
        var tokens = RuleTokens.Matches(text).Cast<Match>().ToArray();
        foreach (var match in tokens)
        {
            label.AddText(text.Substring(offset, match.Index - offset));
            var number = match.Groups["number"].Success;
            var color = number ? NumberColor(text, match, tokens) : AttributePalette.Find(AttributePalette.TextKey(match.Value));
            if (!number && color is null) color = new Color("83cec6");
            if (color is not null) label.PushColor(color.Value);
            var bold = !number || emphasizeNumbers;
            if (bold) label.PushFont(MatchTheme.Font(bold: true));
            label.AddText(match.Value);
            if (bold) label.Pop();
            if (color is not null) label.Pop();
            offset = match.Index + match.Length;
        }
        label.AddText(text.Substring(offset));
    }

    // 只给同一短语中有明确属性关联的数值着色；等级、数量和任务次数保持正文色。
    internal static Color? NumberColor(string text, Match number, Match[] tokens)
    {
        var tail = text[(number.Index + number.Length)..];
        if (Regex.IsMatch(tail, @"^\s*(级|次|张|件|个|回合|轮)")) return null;
        return tokens.Where(token => !token.Groups["number"].Success)
            .Select(token => new { Token = token, Color = AttributePalette.Find(AttributePalette.TextKey(token.Value)),
                Distance = token.Index < number.Index ? number.Index - token.Index - token.Length : token.Index - number.Index - number.Length })
            .Where(candidate => candidate.Color is not null && candidate.Distance <= 16
                && !Regex.IsMatch(text.Substring(Math.Min(candidate.Token.Index + candidate.Token.Length, number.Index + number.Length),
                    Math.Max(0, candidate.Distance)), "[，。；;\\n]"))
            .OrderBy(candidate => candidate.Distance).Select(candidate => candidate.Color).FirstOrDefault();
    }
}
