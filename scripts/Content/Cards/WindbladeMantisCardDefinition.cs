using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 风刃螳螂莫娜卡牌，提供随机疾速及出售风属性冷却奖励（内容层）。
public sealed class WindbladeMantisCardDefinition : CardDefinition
{
    public WindbladeMantisCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.windblade_mantis"), "风刃螳螂", new StringName("mona"), CardSize.Small, [GameElements.Wind],
            illustration: new StringName("res://art/ui/card-face/artwork/windblade_mantis-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "己方随机1张卡牌获得1/2/3/4秒疾速。"),
             new(CardKeywords.Sell, "己方最左侧风属性卡牌冷却永久缩减1/2/3/4%。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Insect]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hasten_random_ally"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 40,
            [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, level * 10)])],
        onSellReward: new ReduceLeftmostElementCardCooldownOnSellDefinition(GameElements.Wind, level));
}
