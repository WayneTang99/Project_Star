using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 静水蝎莫娜卡牌，复用属性伤害与出售水属性冷却奖励（内容层）。
public sealed class StillwaterScorpionCardDefinition : CardDefinition
{
    public StillwaterScorpionCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.stillwater_scorpion"), "静水蝎", new StringName("mona"), CardSize.Small, [GameElements.Water],
            illustration: new StringName("res://art/ui/card-face/artwork/stillwater_scorpion-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "造成10/20/40/80伤害。"),
             new(CardKeywords.Sell, "己方最左侧水属性卡牌冷却永久缩减1/2/3/4%。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Insect]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 10 << (level - 1) },
        [new AbilityDefinition(new StringName("ability.attribute_damage"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 40, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
        onSellReward: new ReduceLeftmostElementCardCooldownOnSellDefinition(GameElements.Water, level));
}
