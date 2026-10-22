using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 元素融合怪莫娜卡牌，按每种元素首次发动倍增本场攻击（内容层）。
public sealed class ElementalAmalgamCardDefinition : CardDefinition
{
    public ElementalAmalgamCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.elemental_amalgam"), "元素融合怪", new StringName("mona"), CardSize.Large, [GameElements.General],
        setKey: new StringName("set.elementalist"), illustration: new StringName("res://art/ui/card-face/artwork/elemental_amalgam-illustration-navy-20261022.png"),
        descriptionEntries: [new(CardKeywords.Activate, "造成40伤害。"),
            new(CardKeywords.Echo, "己方每种非通用元素的卡牌首次发动时，此卡牌攻击翻倍。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 90 })),
        new TagSet([]), initialLevel: 4,
        levels: [new CardLevelDefinition(4, new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 40 },
            [new AbilityDefinition(new StringName("ability.attribute_damage"), AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 90, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
             new AbilityDefinition(new StringName("ability.double_attack_on_first_element"), AbilityActivation.EchoOnFirstAlliedElementActivated,
                AbilityTarget.SelfCard, 0, 0, [new MultiplySourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, 2)])])]) { }
}
