using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 辰光之门圣骑士卡牌内容定义（内容层）。
public sealed class DawnlightGateCardDefinition : CardDefinition
{
    public DawnlightGateCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.dawnlight_gate"), "辰光之门", new StringName("paladin"),
                CardSize.Medium, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/dawnlight_gate-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Echo, "当己方光属性卡牌发动后，己方随机1张其他战场光属性卡牌获得0.5/1秒充能（排除本次发动的卡牌）。")])),
            new TagSet([GameTags.Location]), initialLevel: 3,
            levels: [CreateLevel(3, 5), CreateLevel(4, 10)]) { }

    private static CardLevelDefinition CreateLevel(int level, int ticks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.allied_light_activation_charge_echo"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ChargeRandomOtherAlliedElementCardEffectDefinition(GameElements.Light, ticks, ExcludeEventCard: true)],
            triggerCardElement: GameElements.Light)]);
}
