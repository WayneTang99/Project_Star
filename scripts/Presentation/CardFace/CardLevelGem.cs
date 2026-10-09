using System.Collections.Generic;
using Godot;

namespace Project_Star.Presentation.CardFace;

// 等级晶体按 HTML 的金色菱形底座与等级色渐变绘制（表现层）。
public sealed partial class CardLevelGem : Control
{
    private int _level = 1;
    private static readonly Dictionary<int, Texture2D> Textures = new();
    // 等级仅改变视觉色彩，不承载可变玩法状态。
    public void SetLevel(int level) { _level = Mathf.Clamp(level, 1, 5); QueueRedraw(); }
    public override void _Draw()
    {
        if (!Textures.TryGetValue(_level, out var texture))
        {
            using var image = new Image();
            var rarity = LevelColor(_level).ToHtml(false);
            image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='72' height='72' viewBox='0 0 36 36'><defs><linearGradient id='a' x2='1' y2='1'><stop stop-color='#e7e2b6'/><stop offset='1' stop-color='#816f40'/></linearGradient><linearGradient id='b' x2='1' y2='1'><stop stop-color='#fff' stop-opacity='.73'/><stop offset='.5' stop-color='#{rarity}'/><stop offset='1' stop-color='#141417' stop-opacity='.33'/></linearGradient></defs><path d='M18 0 36 18 18 36 0 18Z' fill='#46381e'/><path d='M18 1 35 18 18 35 1 18Z' fill='url(#a)' stroke='#fff0bf'/><path d='M18 5 31 18 18 31 5 18Z' fill='url(#b)' stroke='#ffffff77'/></svg>");
            texture = ImageTexture.CreateFromImage(image); Textures.Add(_level, texture);
        }
        DrawTextureRect(texture, new Rect2(Vector2.Zero, Size), false);
    }
    // 与 HTML 等级配色表保持一致。
    public static Color LevelColor(int level) => new(level switch { 2 => "67b0dc", 3 => "ac86da", 4 => "e3a152", 5 => "d46b65", _ => "bfc1b4" });
}
