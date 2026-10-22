using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 巫师之靴莫娜装备定义，暗属性召唤流派（内容层）。
public sealed class MagisterBootsCardDefinition : CardDefinition
{
    public MagisterBootsCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.magister_boots"), "巫师之靴", new StringName("mona"), CardSize.Small, [GameElements.Dark],
        setKey: new StringName("set.wizard"), illustration: new StringName("res://art/ui/card-face/artwork/magister_boots-illustration-dark-red.png"), descriptionEntries:
        [new(CardKeywords.Echo, "当己方暗属性卡牌被召唤后，该卡牌获得1/2/3秒疾速。")])),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int hasteTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hasten_summoned_dark_card"), AbilityActivation.EchoOnAlliedCardSummoned,
            AbilityTarget.EventCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, hasteTicks)],
            triggerCardElement: GameElements.Dark)]);
}
