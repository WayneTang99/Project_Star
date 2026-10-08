using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 传动齿轮组哈尔拉卡牌内容定义（内容层）。
public sealed class TransmissionGearAssemblyCardDefinition : CardDefinition
{
    public TransmissionGearAssemblyCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.transmission_gear_assembly"), "传动齿轮组", new StringName("harla"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/transmission_gear_assembly-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Echo, "当左侧直接相邻的己方战场卡牌发动后，右侧直接相邻的己方战场卡牌获得1/2秒充能。")])),
            new TagSet([GameTags.Mechanical]), initialLevel: 3,
            levels: [CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.adjacent_activation_charge"),
            AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.RightAdjacentAlliedCard, 0, 0,
            [new ChargeCardEffectDefinition((level - 2) * 10)], triggerCardSide: AdjacentCardSide.Left)]);
}
