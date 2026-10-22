using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 元素腰带莫娜装备，恢复魔法并在己方发动后随机改变本场元素（内容层）。
public sealed class ElementalBeltCardDefinition : CardDefinition
{
    public ElementalBeltCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.elemental_belt"), "元素腰带", new StringName("mona"), CardSize.Medium, [GameElements.General],
        setKey: new StringName("set.elementalist"), illustration: new StringName("res://art/ui/card-face/artwork/elemental_belt-illustration-navy-20261022.png"),
        descriptionEntries: [new(CardKeywords.Activate, "恢复10/20魔法。"),
            new(CardKeywords.Echo, "己方卡牌发动时，此卡牌元素变为随机非通用元素。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 })),
        new TagSet([GameTags.Equipment]), initialLevel: 3, levels: [CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int mana) => new(level, null,
        [new AbilityDefinition(new StringName("ability.restore_mana"), AbilityActivation.Active, AbilityTarget.AlliedHero,
            0, 50, [new RestoreManaEffectDefinition(mana)]),
         new AbilityDefinition(new StringName("ability.randomize_element_on_allied_activation"), AbilityActivation.EchoOnMatchingAlliedCardActivated,
            AbilityTarget.SelfCard, 0, 0, [new RandomizeSourceElementEffectDefinition()])]);
}
