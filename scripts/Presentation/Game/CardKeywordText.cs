using System.Text.RegularExpressions;
using System;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Game;

// 表现层为卡牌文本中的效果关键词着色，保留原始描述及字面文本。
internal static class CardKeywordText
{
    private static readonly Regex RuleTokens = new(@"(?<cardReference>【[^】\r\n]+】)|(?<identity>\[?(?:无属性|[火水风土雷木冰光暗]属性|[火水风土雷木冰光暗](?=卡牌|攻击)|小型|中型|大型|装备|陷阱|材料|消耗品|人类|野兽|水生物|虫族|载具|龙族|机械|恶魔|亡灵|建筑|地域|植物|精灵)\]?)|(?<cooldown>冷却(?:时间)?\s*[−－+＋-]\s*[0-9]+(?:\.[0-9]+)?(?:%|秒)?)|冷却减少|减少冷却|缩短冷却|降低冷却|冷却增加|增加冷却|延长冷却|提高冷却|生命再生|魔法再生|最大生命值|最大生命|生命值|生命|最大魔法值|最大魔法|恢复魔法|回复魔法|普通伤害|治疗加成|疾速时长加成|魔法回复|魔法|再生|价值|金币|金钱|发动|回响|亡语|任务|光环|拾取|轮初|升级|开战|历战|凯旋|被动|消耗|出售|交易|攻击|伤害|护甲|治疗|中毒|灼伤|疾速|迟缓|飞行|狂暴|隐身|修复|摧毁|转变|召唤|充能|禁锢|多重|Poison|Burn|Haste|Slow|Immobilize|(?<number>[0-9]+(?:\.[0-9]+)?%?)");

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
            var identity = match.Groups["identity"].Success;
            var cardReference = match.Groups["cardReference"].Success;
            var color = cardReference ? new Color("c5ad87") : identity ? (emphasizeNumbers ? new Color("c5ad87") : (Color?)null)
                : number ? NumberColor(text, match, tokens) : TokenColor(match);
            if (!number && !identity && color is null) color = new Color("83cec6");
            if (color is not null) label.PushColor(color.Value);
            var bold = cardReference || (identity ? emphasizeNumbers : !number || emphasizeNumbers);
            if (bold) label.PushFont(label.GetThemeFont("bold_font"));
            label.AddText(identity && emphasizeNumbers ? "[" + match.Value.Trim('[', ']') + "]" : match.Value);
            if (bold) label.Pop();
            if (color is not null) label.Pop();
            offset = match.Index + match.Length;
        }
        label.AddText(text.Substring(offset));
    }

    private static Color? TokenColor(Match token) => token.Groups["cooldown"].Success
        ? AttributePalette.Find(Regex.IsMatch(token.Value, "[−－-]") ? AttributePalette.Haste : AttributePalette.Slow)
        : AttributePalette.Find(AttributePalette.TextKey(token.Value));

    // 只给同一短语中有明确属性关联的数值着色；等级、数量和任务次数保持正文色。
    internal static Color? NumberColor(string text, Match number, Match[] tokens)
    {
        var tail = text[(number.Index + number.Length)..];
        if (Regex.IsMatch(tail, @"^\s*(级|次|张|件|个|回合|轮)")) return null;
        return tokens.Where(token => !token.Groups["number"].Success)
            .Select(token => new { Token = token, Color = TokenColor(token),
                Distance = token.Index < number.Index ? number.Index - token.Index - token.Length : token.Index - number.Index - number.Length })
            .Where(candidate => candidate.Color is not null && candidate.Distance <= 16
                && !Regex.IsMatch(text.Substring(Math.Min(candidate.Token.Index + candidate.Token.Length, number.Index + number.Length),
                    Math.Max(0, candidate.Distance)), "[，。；;\\n]"))
            .OrderBy(candidate => candidate.Distance).Select(candidate => candidate.Color).FirstOrDefault();
    }
}
