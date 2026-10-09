using System.Text.RegularExpressions;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 表现层为卡牌文本中的效果关键词着色，保留原始描述及字面文本。
internal static class CardKeywordText
{
    private static readonly Regex Keywords = new("发动|光环|被动|消耗|出售|攻击|护甲|治疗|中毒|灼伤|疾速|充能|禁锢");

    // 用富文本颜色栈显示纯文本，避免将内容中的括号解释为BBCode。
    public static void Render(RichTextLabel label, string text)
    {
        label.Clear();
        Append(label, text);
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

    private static void Append(RichTextLabel label, string text)
    {
        var offset = 0;
        foreach (Match match in Keywords.Matches(text))
        {
            label.AddText(text.Substring(offset, match.Index - offset));
            label.PushColor(new Color("83cec6"));
            label.PushFont(MatchTheme.Font(bold: true));
            label.AddText(match.Value);
            label.Pop();
            label.Pop();
            offset = match.Index + match.Length;
        }
        label.AddText(text.Substring(offset));
    }
}
