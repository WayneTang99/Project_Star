using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 学者之书莫娜卡牌，在战场或备战区提供轮初同级随机技能（内容层）。
public sealed class ScholarsBookCardDefinition : CardDefinition
{
    public ScholarsBookCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.scholars_book"), "学者之书", new StringName("mona"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/scholars_book-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.RoundStart, "在战场区或备战区时，随机学习1个任意英雄的同级技能，包含当前英雄，重复获取按技能合并规则处理。")])),
            new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.round_random_hero_skill"), AbilityActivation.PassiveOnRoundStart,
            AbilityTarget.AlliedHero, 0, 0, [new GrantRandomHeroSourceLevelSkillEffectDefinition()], allowsBench: true)]);
}
