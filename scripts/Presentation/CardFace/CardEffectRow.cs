using Godot;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.CardFace;

// 卡面效果行，紧凑字号按实际布局设置。
public sealed partial class CardEffectRow : HBoxContainer
{
    private TextureRect _icon = null!;
    private Label _value = null!;
    private readonly StyleBoxFlat _plate = new()
    {
        BgColor = new Color("#9b3433"),
        BorderColor = new Color("#e9c77f"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
    };

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _value = GetNode<Label>("Value");
    }

    private string _kind = "damage";
    public override void _Draw() => DrawStyleBox(MatchTheme.Plate("effect-" + _kind, 3), new Rect2(new Vector2(-5, 0), Size + new Vector2(12, 0)));

    // 按效果语义选择图标与显示值。
    public void Configure(CardFaceEffect effect)
    {
        var iconPath = effect.Kind switch
        {
            CardFaceEffectKind.Damage => "res://art/ui/card-face/icons/damage.svg",
            CardFaceEffectKind.Healing => "res://art/ui/card-face/icons/healing.svg",
            CardFaceEffectKind.Armor => "res://art/ui/card-face/icons/armor.svg",
            CardFaceEffectKind.Poison => "res://art/ui/card-face/icons/poison.svg",
            CardFaceEffectKind.Burn => "res://art/ui/card-face/icons/burn.svg",
            CardFaceEffectKind.Mana => "res://art/ui/card-face/icons/mana.svg",
            _ => "res://art/ui/card-face/icons/damage.svg",
        };
        _kind = effect.Kind.ToString().ToLowerInvariant();
        _icon.Texture = MatchTheme.Icon(new StringName(_kind));
        _value.Text = effect.Value;
        _plate.BgColor = new Color(effect.Kind switch
        {
            CardFaceEffectKind.Healing => "#527d3e",
            CardFaceEffectKind.Armor => "#947027",
            CardFaceEffectKind.Poison => "#3c7344",
            CardFaceEffectKind.Burn => "#a55d29",
            CardFaceEffectKind.Mana => "#326d84",
            _ => "#9b3433",
        });
        _value.AddThemeColorOverride("font_color", new Color("fff2cd"));
        _value.AddThemeColorOverride("font_outline_color", new Color("241810"));
        MatchTheme.Text(_value, 15, new Color("fff5db"), true, true);
    }

    // 缩小装饰留白并保留文字下限，长值完整内容由详情承载。
    public void SetDisplayMetrics(int font, float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        _icon.CustomMinimumSize = new Vector2(12, 12);
        _icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddThemeConstantOverride("separation", 4);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.ClipText = true; _value.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        QueueRedraw();
    }

    // 两位数使用真实字体宽度，长公式超过可用宽度时才省略。
    public float PreferredWidth(int font) => Mathf.Max(40, 5 + 12 + 4 + MatchTheme.Font(true, true).GetStringSize(_value.Text, fontSize: font).X + 7);
}
