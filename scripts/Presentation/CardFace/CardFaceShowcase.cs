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
    private HBoxContainer? _compactCards;

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

        if (OS.GetCmdlineUserArgs().Contains("--capture-overlay"))
        {
            var caption = new Label
            {
                Text = "紧凑展示（中型夹具：双元素、三孔、多效果、长数值）",
                Position = new Vector2(16, 464),
            };
            AddChild(caption);
            _compactCards = new HBoxContainer { Position = new Vector2(16, 494) };
            _compactCards.AddThemeConstantOverride("separation", 16);
            AddChild(_compactCards);
        }

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

    // 捕获原生与紧凑卡面，双元素、孔位及效果溢出仅使用展示夹具。
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

    private void AddCard(HBoxContainer cards, CardFaceViewModel model)
    {
        var card = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFace>();
        cards.AddChild(card);
        card.SetCard(model);
        if (_compactCards is null) return;
        var compactModel = model.Size == CardSize.Medium
            ? new CardFaceViewModel(model.CardKey, model.DisplayName, model.FactionKey, model.Size, model.Level, 120,
                model.Artwork, [GameElements.Fire, GameElements.Wood],
                [new(CardFaceEffectKind.Damage, "1000"), new(CardFaceEffectKind.Poison, "20"),
                    new(CardFaceEffectKind.Armor, "30"), new(CardFaceEffectKind.Healing, "40"), new(CardFaceEffectKind.Mana, "50")])
                { GemNames = new string?[] { null, "展示宝石", null } }
            : model;
        var compact = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<CardFace>();
        _compactCards.AddChild(compact);
        compact.SetCard(compactModel);
        compact.SetDisplaySize(new Vector2(90 * (int)model.Size, 180));
        compact.CustomMinimumSize = compact.Size;
    }
}
