using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔导士法环莫娜装备定义（内容层）。
public sealed class MageCircletCardDefinition : CardDefinition
{
    public MageCircletCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.mage_circlet"), "魔导士法环", new StringName("mona"), CardSize.Medium, [GameElements.General],
        setKey: new StringName("set.mage"), illustration: new StringName("res://art/ui/card-face/artwork/mage_circlet-illustration.png"),
        descriptionEntries: [new(CardKeywords.Activate, "消耗100魔法，造成等同于己方最大魔法值＋魔法再生×10/20的伤害。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 } )),
        new TagSet([GameTags.Equipment]), initialLevel: 3,
        levels: [CreateLevel(3, 60, 10), CreateLevel(4, 50, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.mage_circlet"), AbilityActivation.Active, AbilityTarget.EnemyHero, 100, cooldown, [new HeroManaDamageEffectDefinition(multiplier)])]);
}
