using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.CardFace;

// 从正式卡牌定义与插画属性组成三种尺寸的卡面示例（表现层）。
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
        var adapter = new CardDisplayAdapter();
        var armguardLevel = armguard.GetLevel(armguard.InitialLevel)!;
        AddCard(cards, new CardFaceViewModel(
            armguard.Attributes.Identity.Key,
            armguard.Attributes.Identity.DisplayName,
            armguard.Attributes.Identity.FactionKey,
            armguard.Attributes.Identity.Size,
            armguard.InitialLevel,
            CardValueCalculator.CalculateInitialValue(armguard, armguard.InitialLevel),
            adapter.Artwork(armguard.Attributes.Identity.Illustration),
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
            adapter.Artwork(boar.Attributes.Identity.Illustration),
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
            adapter.Artwork(hammer.Attributes.Identity.Illustration),
            hammer.Attributes.Identity.ElementKeys,
            [new CardFaceEffect(CardFaceEffectKind.Damage, $"{damagePercent}%")]));
        if (OS.GetCmdlineUserArgs().Contains("--capture-overlay"))
            Callable.From(CaptureOverlay).CallDeferred();
    }

    // 捕获原生400高卡面，核对三种尺寸共用整张插画底图与叠加信息。
    private async void CaptureOverlay()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://docs/quality/card-overlay");
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng($"res://docs/quality/card-overlay/native-{image.GetWidth()}x{image.GetHeight()}.png");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private static void AddCard(HBoxContainer cards, CardFaceViewModel model)
    {
        var card = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFace>();
        cards.AddChild(card);
        card.SetCard(model);
    }
}
