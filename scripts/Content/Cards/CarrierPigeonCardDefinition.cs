using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 信鸽罗宾卡牌定义，组合首位发动回响与末位疾速（内容层）。
public sealed class CarrierPigeonCardDefinition : CardDefinition
{
    public CarrierPigeonCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.carrier_pigeon"), "信鸽", new StringName("robin"),
            CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/carrier_pigeon-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Echo, "当己方战场最左侧卡牌发动后，己方战场最右侧卡牌获得1/2/3秒疾速。")]
        )), new TagSet([GameTags.Beast]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int durationTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hasten_rightmost_on_leftmost_activation"),
            AbilityActivation.EchoOnLeftmostAlliedCardActivated, AbilityTarget.RightmostAlliedCard, 0, 0,
            [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, durationTicks)])]);
}
