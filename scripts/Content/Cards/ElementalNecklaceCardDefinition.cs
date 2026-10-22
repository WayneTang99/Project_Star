using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 元素项链莫娜装备，发动回响为不同元素的随机卡牌施加疾速（内容层）。
public sealed class ElementalNecklaceCardDefinition : CardDefinition
{
    public ElementalNecklaceCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.elemental_necklace"), "元素项链", new StringName("mona"), CardSize.Small, [GameElements.General],
        setKey: new StringName("set.elementalist"), illustration: new StringName("res://art/ui/card-face/artwork/elemental_necklace-illustration-navy-20261022.png"),
        descriptionEntries: [new(CardKeywords.Echo, "己方卡牌发动时，己方随机1/2/3张不含该卡牌元素的卡牌获得1秒疾速。") ])),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, new Dictionary<StringName, int>(),
        [new AbilityDefinition(new StringName("ability.haste_different_elements"), AbilityActivation.EchoOnMatchingAlliedCardActivated,
            AbilityTarget.SelfCard, 0, 0,
            [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, TargetCount: level - 1, ExcludeEventCardElements: true)])]);
}
