using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔导士之戒莫娜装备定义（内容层）。
public sealed class MageRingCardDefinition : CardDefinition
{
    public MageRingCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.mage_ring"), "魔导士之戒", new StringName("mona"), CardSize.Small, [GameElements.General],
        setKey: new StringName("set.mage"), illustration: new StringName("res://art/ui/card-face/artwork/mage_ring-illustration.png"),
        descriptionEntries: [new(CardKeywords.Echo, "己方通过发动效果获得魔法再生时，获得20/40最大魔法值。")])),
        new TagSet([GameTags.Equipment]), initialLevel: 3,
        levels: [CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.mage_ring"), AbilityActivation.EchoOnAlliedManaRegenGained, AbilityTarget.AlliedHero, 0, 0, [new IncreaseHeroMaxManaEffectDefinition(amount)])]);
}
