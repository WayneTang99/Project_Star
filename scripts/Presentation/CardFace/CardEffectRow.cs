using Godot;

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

    public override void _Draw() => DrawStyleBox(_plate, new Rect2(Vector2.Zero, Size));

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
        _icon.Texture = GD.Load<Texture2D>(iconPath);
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
    }

    // 缩小装饰留白并保留文字下限，长值完整内容由详情承载。
    public void SetDisplayMetrics(int font, float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        _icon.CustomMinimumSize = Vector2.One * Mathf.Min(font * .7f, height - 2);
        AddThemeConstantOverride("separation", 1);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.ClipText = true; _value.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        QueueRedraw();
    }
}
