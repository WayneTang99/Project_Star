using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 学者长袍莫娜装备定义，按技能实例数量缩放（内容层）。
public sealed class ScholarRobeCardDefinition : CardDefinition
{
    public ScholarRobeCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.scholar_robe"), "学者长袍", new StringName("mona"), CardSize.Medium, [GameElements.General],
        setKey: new StringName("set.scholar"), illustration: new StringName("res://art/ui/card-face/artwork/scholar_robe-illustration-dark-green.png"),
        descriptionEntries: [new(CardKeywords.Activate, "造成等同于己方技能数量×40/80的伤害。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50, [GameAttributeKeys.AttackDamage] = 0 })),
        new TagSet([GameTags.Equipment]), initialLevel: 3,
        levels: [CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.scholar_robe"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 50, [new SkillCountDamageEffectDefinition(multiplier)])]);
}
