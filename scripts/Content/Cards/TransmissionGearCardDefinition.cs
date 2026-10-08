using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 传动齿轮哈尔拉卡牌内容定义（内容层）。
public sealed class TransmissionGearCardDefinition : CardDefinition
{
    public TransmissionGearCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.transmission_gear"), "传动齿轮", new StringName("harla"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/transmission_gear-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Echo, "当左侧直接相邻的己方战场卡牌发动后，右侧直接相邻的己方战场卡牌获得1/2/3/4秒疾速。")])),
            new TagSet([GameTags.Mechanical]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.adjacent_activation_haste"),
            AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.RightAdjacentAlliedCard, 0, 0,
            [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, level * 10)],
            triggerCardSide: AdjacentCardSide.Left)]);
}
