using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 学者眼镜莫娜装备定义，按技能实例数量缩放（内容层）。
public sealed class ScholarSpectaclesCardDefinition : CardDefinition
{
    public ScholarSpectaclesCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.scholar_spectacles"), "学者眼镜", new StringName("mona"), CardSize.Small, [GameElements.General],
        setKey: new StringName("set.scholar"), illustration: new StringName("res://art/ui/card-face/artwork/scholar_spectacles-illustration-dark-green.png"),
        descriptionEntries: [new(CardKeywords.Aura, "获得等同于己方技能数量×2/3/4的魔法再生。")])),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2, 2), CreateLevel(3, 3), CreateLevel(4, 4)]) { }

    private static CardLevelDefinition CreateLevel(int level, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.scholar_spectacles"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0, [new SkillCountManaRegenAuraEffectDefinition(multiplier)])]);
}
