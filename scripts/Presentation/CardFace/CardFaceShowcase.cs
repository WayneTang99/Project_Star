using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.CardFace;

/// <summary>Visual preview assembled from existing card definitions and original sample art.</summary>
public sealed partial class CardFaceShowcase : Control
{
    public override void _Ready()
    {
        var background = new ColorRect
        {
            Color = new Color("#89857f"),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var cards = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        cards.AddThemeConstantOverride("separation", 16);
        center.AddChild(cards);

        var armguard = new ArmguardCardDefinition();
        var armguardLevel = armguard.GetLevel(armguard.InitialLevel)!;
        AddCard(cards, new CardFaceViewModel(
            armguard.Attributes.Identity.Key,
            armguard.Attributes.Identity.DisplayName,
            armguard.Attributes.Identity.FactionKey,
            armguard.Attributes.Identity.Size,
            armguard.InitialLevel,
            CardValueCalculator.CalculateInitialValue(armguard, armguard.InitialLevel),
            GD.Load<Texture2D>("res://art/ui/card-face/artwork/armguard.png"),
            armguard.Attributes.Identity.ElementKeys,
            [
                new CardFaceEffect(CardFaceEffectKind.Damage, armguardLevel.BaseCombatValues[GameAttributeKeys.AttackDamage].ToString()),
                new CardFaceEffect(CardFaceEffectKind.Armor, armguardLevel.BaseCombatValues[GameAttributeKeys.Armor].ToString()),
            ]));

        var boar = new BoarCardDefinition();
        var boarLevel = boar.GetLevel(boar.InitialLevel)!;
        AddCard(cards, new CardFaceViewModel(
            boar.Attributes.Identity.Key,
            boar.Attributes.Identity.DisplayName,
            boar.Attributes.Identity.FactionKey,
            boar.Attributes.Identity.Size,
            boar.InitialLevel,
            CardValueCalculator.CalculateInitialValue(boar, boar.InitialLevel),
            GD.Load<Texture2D>("res://art/ui/card-face/artwork/boar.png"),
            boar.Attributes.Identity.ElementKeys,
            [new CardFaceEffect(CardFaceEffectKind.Damage,
                boarLevel.BaseCombatValues[GameAttributeKeys.AttackDamage].ToString())]));

        var hammer = new JudgmentHammerCardDefinition();
        var hammerLevel = hammer.GetLevel(hammer.InitialLevel)!;
        var damagePercent = hammerLevel.Abilities
            .SelectMany(ability => ability.Effects)
            .OfType<MaxHealthPercentDamageEffectDefinition>()
            .Single()
            .Percent;
        AddCard(cards, new CardFaceViewModel(
            hammer.Attributes.Identity.Key,
            hammer.Attributes.Identity.DisplayName,
            hammer.Attributes.Identity.FactionKey,
            hammer.Attributes.Identity.Size,
            hammer.InitialLevel,
            CardValueCalculator.CalculateInitialValue(hammer, hammer.InitialLevel),
            GD.Load<Texture2D>("res://art/ui/card-face/artwork/judgment_hammer.png"),
            hammer.Attributes.Identity.ElementKeys,
            [new CardFaceEffect(CardFaceEffectKind.Damage, $"{damagePercent}%")]));
    }

    private static void AddCard(HBoxContainer cards, CardFaceViewModel model)
    {
        var card = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFace>();
        cards.AddChild(card);
        card.SetCard(model);
    }
}
