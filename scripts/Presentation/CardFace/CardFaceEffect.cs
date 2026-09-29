namespace Project_Star.Presentation.CardFace;

public enum CardFaceEffectKind
{
    Damage,
    Healing,
    Armor,
    Poison,
    Burn,
}

public sealed record CardFaceEffect(CardFaceEffectKind Kind, string Value);
