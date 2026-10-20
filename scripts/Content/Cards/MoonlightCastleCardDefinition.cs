using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 月光古堡莫娜建筑地域卡牌定义，提供暗属性多重光环（内容层）。
public sealed class MoonlightCastleCardDefinition : CardDefinition
{
    public MoonlightCastleCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.moonlight_castle"), "月光古堡", new StringName("mona"),
            CardSize.Large, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/moonlight_castle-illustration.png"),
            descriptionEntries: [new(CardKeywords.Aura, "己方暗属性卡牌多重增加1。")])),
            new TagSet([GameTags.Building, GameTags.Location]), initialLevel: 4,
            levels: [new CardLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.dark_multicast_aura"), AbilityActivation.PassiveAura,
                    AbilityTarget.SelfCard, 0, 0,
                    [new GrantMulticastToAlliedElementCardsEffectDefinition(GameElements.Dark, 1)])])]) { }
}
