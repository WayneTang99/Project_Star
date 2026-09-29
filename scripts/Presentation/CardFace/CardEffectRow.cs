using Godot;

namespace Project_Star.Presentation.CardFace;

public sealed partial class CardEffectRow : HBoxContainer
{
    private TextureRect _icon = null!;
    private Label _value = null!;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _value = GetNode<Label>("Value");
    }

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
}
