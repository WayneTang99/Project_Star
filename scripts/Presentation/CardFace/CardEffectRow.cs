using Godot;

namespace Project_Star.Presentation.CardFace;

// 卡面效果行，紧凑字号按实际布局设置。
public sealed partial class CardEffectRow : HBoxContainer
{
    private TextureRect _icon = null!;
    private Label _value = null!;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _value = GetNode<Label>("Value");
    }

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
            _ => "res://art/ui/card-face/icons/damage.svg",
        };
        _icon.Texture = GD.Load<Texture2D>(iconPath);
        _value.Text = effect.Value;
    }

    // 缩小装饰留白并保留文字下限，长值完整内容由详情承载。
    public void SetDisplayMetrics(int font, float height)
    {
        CustomMinimumSize = new Vector2(0, height);
        _icon.CustomMinimumSize = Vector2.One * Mathf.Min(16, height - 2);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.ClipText = true; _value.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }
}
