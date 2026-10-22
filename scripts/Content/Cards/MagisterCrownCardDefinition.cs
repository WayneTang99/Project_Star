using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 巫师之冠莫娜装备定义，暗属性召唤流派（内容层）。
public sealed class MagisterCrownCardDefinition : CardDefinition
{
    public MagisterCrownCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.magister_crown"), "巫师之冠", new StringName("mona"), CardSize.Small, [GameElements.Dark],
        setKey: new StringName("set.wizard"), illustration: new StringName("res://art/ui/card-face/artwork/magister_crown-illustration-dark-red.png"), descriptionEntries:
        [new(CardKeywords.Summon, "战斗开始时，若己方战场有空位，随机召唤1张同级暗属性小型卡牌，候选为莫娜或无阵营卡牌。")])),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.summon_random_dark_small"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new SummonRandomMatchingCardEffectDefinition(GameElements.Dark, CardSize.Small)])]);
}
