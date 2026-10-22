using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 学者披肩莫娜装备定义，按技能实例数量缩放（内容层）。
public sealed class ScholarMantleCardDefinition : CardDefinition
{
    public ScholarMantleCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.scholar_mantle"), "学者披肩", new StringName("mona"), CardSize.Small, [GameElements.General],
        setKey: new StringName("set.scholar"), illustration: new StringName("res://art/ui/card-face/artwork/scholar_mantle-illustration-dark-green.png"),
        descriptionEntries: [new(CardKeywords.Activate, "获得等同于己方技能数量×20/30/40的最大魔法值。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 })),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2, 20), CreateLevel(3, 30), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.scholar_mantle"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 50, [new SkillCountMaxManaEffectDefinition(multiplier)])]);
}
