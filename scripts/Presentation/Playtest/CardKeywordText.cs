using System.Text.RegularExpressions;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 表现层为卡牌文本中的效果关键词着色，保留原始描述及字面文本。
internal static class CardKeywordText
{
    private static readonly Regex Keywords = new("攻击|护甲|治疗|中毒|灼伤|疾速|充能|禁锢");

    // 用富文本颜色栈显示纯文本，避免将内容中的括号解释为BBCode。
    public static void Render(RichTextLabel label, string text)
    {
        label.Clear();
        var offset = 0;
        foreach (Match match in Keywords.Matches(text))
        {
            label.AddText(text.Substring(offset, match.Index - offset));
            label.PushColor(new Color(match.Value switch
            {
                "攻击" => "d74747",
                "护甲" => "d9ad16",
                "治疗" => "83c76c",
                "中毒" => "28783b",
                "灼伤" => "e87e24",
                "疾速" or "充能" => "20a6b5",
                "禁锢" => "9852bd",
                _ => "243d52",
            }));
            label.AddText(match.Value);
            label.Pop();
            offset = match.Index + match.Length;
        }
        label.AddText(text.Substring(offset));
    }
}
