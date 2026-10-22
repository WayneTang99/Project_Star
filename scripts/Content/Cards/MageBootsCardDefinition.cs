using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔导士之靴莫娜装备定义（内容层）。
public sealed class MageBootsCardDefinition : CardDefinition
{
    public MageBootsCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.mage_boots"), "魔导士之靴", new StringName("mona"), CardSize.Small, [GameElements.General],
        setKey: new StringName("set.mage"), illustration: new StringName("res://art/ui/card-face/artwork/mage_boots-illustration.png"),
        descriptionEntries: [new(CardKeywords.Activate, "消耗20魔法，获得20/40/80最大魔法值。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 } )),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.mage_boots"), AbilityActivation.Active, AbilityTarget.AlliedHero, 20, 50, [new IncreaseHeroMaxManaEffectDefinition(amount)])]);
}
